using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SubscriptionPlatform.Api1.Subscriptions.Data;
using SubscriptionPlatform.Shared.Events;

namespace SubscriptionPlatform.Api1.Subscriptions.BackgroundServices;

/// <summary>
/// CRÍTICO: BackgroundService que procesa mensajes pendientes cuando RabbitMQ vuelve a estar disponible.
/// Respeta ESTRICTO ORDEN CRONOLÓGICO (.OrderBy(m => m.CreatedAt)) según los requerimientos del examen.
/// </summary>
public class PendingMessageProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<PendingMessageProcessor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);
    private IConnection? _rabbitMQConnection;

    public PendingMessageProcessor(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        ILogger<PendingMessageProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 PendingMessageProcessor iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error en PendingMessageProcessor: {ex.Message}");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("🛑 PendingMessageProcessor detenido");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        if (!IsRabbitMQAvailable())
        {
            _logger.LogDebug("⏳ RabbitMQ no disponible. Reintentando en 5s...");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SubscriptionContext>();

        // ⭐ IMPORTANTE: ORDEN CRONOLÓGICO ESTRICTO
        var pendingMessages = await context.PendingMessages
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)  // ORDEN CRONOLÓGICO - NO MODIFICAR
            .ToListAsync(ct);

        if (pendingMessages.Count > 0)
        {
            _logger.LogInformation(
                $"📨 Encontrados {pendingMessages.Count} mensajes pendientes. " +
                $"Procesando en orden cronológico...");
        }

        foreach (var msg in pendingMessages)
        {
            try
            {
                PublishToRabbitMQ(msg.MessageBody);

                msg.IsProcessed = true;
                msg.ProcessedAt = DateTime.UtcNow;
                context.PendingMessages.Update(msg);
                await context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    $"✅ Mensaje procesado: {msg.Id} | " +
                    $"Tipo: {msg.EventType} | " +
                    $"Creado: {msg.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                msg.RetryCount++;
                _logger.LogWarning(
                    $"⚠️  Error procesando mensaje {msg.Id}: {ex.Message} | " +
                    $"Reintento {msg.RetryCount}/3");

                if (msg.RetryCount >= 3)
                {
                    msg.IsProcessed = false;
                    _logger.LogError($"❌ Máximo de reintentos alcanzado para mensaje {msg.Id}");
                }

                context.PendingMessages.Update(msg);
                await context.SaveChangesAsync(ct);
            }
        }
    }

    private void PublishToRabbitMQ(string messageJson)
    {
        IConnection? connection = null;
        try
        {
            connection = _connectionFactory.CreateConnection();

            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: PaymentQueueConfig.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: PaymentQueueConfig.DeclarationArguments);

            var body = System.Text.Encoding.UTF8.GetBytes(messageJson);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: "",
                routingKey: PaymentQueueConfig.QueueName,
                basicProperties: properties,
                body: body);

            _logger.LogDebug("Mensaje reenviado exitosamente a RabbitMQ");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error reenviando a RabbitMQ: {ex.Message}", ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private bool IsRabbitMQAvailable()
    {
        try
        {
            if (_rabbitMQConnection == null || !_rabbitMQConnection.IsOpen)
            {
                _rabbitMQConnection = _connectionFactory.CreateConnection();
            }

            return _rabbitMQConnection?.IsOpen ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"RabbitMQ check falló: {ex.Message}");
            return false;
        }
    }

    public override void Dispose()
    {
        _rabbitMQConnection?.Dispose();
        base.Dispose();
    }
}
