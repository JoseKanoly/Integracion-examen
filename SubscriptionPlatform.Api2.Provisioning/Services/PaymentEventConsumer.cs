using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using SubscriptionPlatform.Api2.Provisioning.Data;
using SubscriptionPlatform.Api2.Provisioning.RabbitMQ;
using SubscriptionPlatform.Shared.Events;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api2.Provisioning.Services;

/// <summary>
/// CRÍTICO: Consumidor que valida eventos y rechaza inválidos automáticamente a DLQ
/// </summary>
public class PaymentEventConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<PaymentEventConsumer> _logger;
    private IConnection? _rabbitMQConnection;
    private IModel? _channel;

    public PaymentEventConsumer(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        ILogger<PaymentEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🎧 PaymentEventConsumer iniciando...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error en PaymentEventConsumer: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("🛑 PaymentEventConsumer detenido");
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        _rabbitMQConnection = _connectionFactory.CreateConnection();

        if (!_rabbitMQConnection.IsOpen)
        {
            throw new Exception("No se pudo establecer conexión a RabbitMQ");
        }

        _logger.LogInformation("✓ Conectado a RabbitMQ");

        _channel = _rabbitMQConnection.CreateModel();

        using (var scope = _scopeFactory.CreateScope())
        {
            var setupLogger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentEventConsumer>>();
            RabbitMQSetup.ConfigureQueues(_rabbitMQConnection, setupLogger);
        }

        _channel.BasicQos(0, 1, false);
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            string messageId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation($"📨 [{messageId}] Evento recibido de RabbitMQ");

                var body = ea.Body.ToArray();
                var json = System.Text.Encoding.UTF8.GetString(body);

                _logger.LogDebug($"Contenido: {json}");

                var paymentEvent = JsonSerializer.Deserialize<PaymentProcessedEvent>(json);

                // VALIDACIÓN CRÍTICA
                if (!ValidatePaymentEvent(paymentEvent))
                {
                    _logger.LogError(
                        $"❌ [{messageId}] Evento inválido detectado. " +
                        $"Enviando a Dead Letter Queue...");

                    await LogInvalidEventAsync(json, messageId);

                    // RECHAZAR A DLQ
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                    return;
                }

                // PROCESAR EVENTO VÁLIDO
                _logger.LogInformation(
                    $"✓ [{messageId}] Evento válido. " +
                    $"Usuario: {paymentEvent!.UserId}, Plan: {paymentEvent.PlanType}");

                await ProcessPaymentAsync(paymentEvent);

                // CONFIRMAR
                _channel.BasicAck(ea.DeliveryTag, false);

                _logger.LogInformation(
                    $"✅ [{messageId}] Evento procesado completamente. " +
                    $"Usuario {paymentEvent.UserId} actualizado con plan {paymentEvent.PlanType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"❌ [{messageId}] Error procesando evento: {ex.Message}\n" +
                    $"Stack: {ex.StackTrace}");

                _channel.BasicNack(ea.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsume(
            queue: PaymentQueueConfig.QueueName,
            autoAck: false,
            consumerTag: "payment-consumer",
            consumer: consumer);

        _logger.LogInformation(
            "🎧 Escuchando eventos en cola 'payment-processed'...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private bool ValidatePaymentEvent(PaymentProcessedEvent? evt)
    {
        if (evt == null)
        {
            _logger.LogWarning("Evento es null");
            return false;
        }

        if (evt.UserId == Guid.Empty)
        {
            _logger.LogWarning("UserId está vacío");
            return false;
        }

        if (evt.Amount <= 0)
        {
            _logger.LogWarning($"Monto inválido: {evt.Amount}");
            return false;
        }

        if (string.IsNullOrEmpty(evt.PlanType))
        {
            _logger.LogWarning("PlanType está vacío");
            return false;
        }

        var validPlans = new[] { "Basic", "Premium", "Enterprise" };
        if (!validPlans.Contains(evt.PlanType))
        {
            _logger.LogWarning($"PlanType inválido: {evt.PlanType}");
            return false;
        }

        if (evt.ProcessedAt == default)
        {
            _logger.LogWarning("ProcessedAt no está establecido");
            return false;
        }

        return true;
    }

    private async Task ProcessPaymentAsync(PaymentProcessedEvent evt)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProvisioningContext>();

        var userAccess = await context.UserAccesses
            .FirstOrDefaultAsync(ua => ua.UserId == evt.UserId);

        if (userAccess == null)
        {
            userAccess = new UserAccess
            {
                Id = Guid.NewGuid(),
                UserId = evt.UserId,
                PlanType = evt.PlanType,
                ActivatedAt = DateTime.UtcNow,
                HasPremiumAccess = evt.PlanType != "Basic",
                Email = evt.Email
            };

            context.UserAccesses.Add(userAccess);
            _logger.LogInformation($"📝 Nuevo acceso creado para usuario {evt.UserId}");
        }
        else
        {
            userAccess.PlanType = evt.PlanType;
            userAccess.HasPremiumAccess = evt.PlanType != "Basic";
            userAccess.ActivatedAt = DateTime.UtcNow;
            context.UserAccesses.Update(userAccess);
            _logger.LogInformation($"🔄 Acceso actualizado para usuario {evt.UserId}");
        }

        await UnlockCoursesForPlanAsync(evt.UserId, evt.PlanType, context);

        await context.SaveChangesAsync();

        _logger.LogInformation(
            $"💾 Cambios guardados en BD. " +
            $"Usuario {evt.UserId} ahora tiene acceso al plan {evt.PlanType}");
    }

    private async Task UnlockCoursesForPlanAsync(Guid userId, string planType, ProvisioningContext context)
    {
        var coursesToUnlock = GetCoursesForPlan(planType);

        _logger.LogInformation(
            $"🔓 Desbloqueando {coursesToUnlock.Length} cursos para plan {planType}");

        foreach (var courseId in coursesToUnlock)
        {
            var existing = await context.CoursePermissions
                .FirstOrDefaultAsync(cp => cp.UserId == userId && cp.CourseId == courseId);

            if (existing == null)
            {
                context.CoursePermissions.Add(new CoursePermission
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CourseId = courseId,
                    IsUnlocked = true,
                    UnlockedAt = DateTime.UtcNow
                });

                _logger.LogDebug($"   ✓ Curso {courseId} desbloqueado");
            }
        }
    }

    private string[] GetCoursesForPlan(string planType)
    {
        return planType switch
        {
            "Basic" => new[]
            {
                "course-001-csharp-basics",
                "course-002-dotnet-intro"
            },
            "Premium" => new[]
            {
                "course-001-csharp-basics",
                "course-002-dotnet-intro",
                "course-003-entity-framework",
                "course-004-asp-net-core"
            },
            "Enterprise" => new[]
            {
                "course-001-csharp-basics",
                "course-002-dotnet-intro",
                "course-003-entity-framework",
                "course-004-asp-net-core",
                "course-005-microservices",
                "course-006-cloud-deployment"
            },
            _ => Array.Empty<string>()
        };
    }

    private async Task LogInvalidEventAsync(string messageBody, string messageId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ProvisioningContext>();

            var dlmEntry = new DeadLetterMessage
            {
                Id = Guid.NewGuid(),
                EventType = nameof(PaymentProcessedEvent),
                MessageBody = messageBody,
                ErrorReason = $"Validación fallida: Evento inválido detectado [{messageId}]",
                ReceivedAt = DateTime.UtcNow
            };

            context.DeadLetterMessages.Add(dlmEntry);
            await context.SaveChangesAsync();

            _logger.LogWarning($"📋 Evento registrado en DeadLetterMessages: {dlmEntry.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error registrando en DLM: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _rabbitMQConnection?.Dispose();
        base.Dispose();
    }
}
