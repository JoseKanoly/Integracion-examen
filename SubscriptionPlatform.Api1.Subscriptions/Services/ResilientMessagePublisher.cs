using RabbitMQ.Client;
using System.Text.Json;
using SubscriptionPlatform.Api1.Subscriptions.Data;
using SubscriptionPlatform.Shared.Events;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api1.Subscriptions.Services;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string queueName) where T : class;
}

/// <summary>
/// CRÍTICO: Publicador resiliente que guarda en BD si RabbitMQ falla
/// </summary>
public class ResilientMessagePublisher : IMessagePublisher
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly SubscriptionContext _context;
    private readonly ILogger<ResilientMessagePublisher> _logger;

    public ResilientMessagePublisher(
        IConnectionFactory connectionFactory,
        SubscriptionContext context,
        ILogger<ResilientMessagePublisher> logger)
    {
        _connectionFactory = connectionFactory;
        _context = context;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, string queueName) where T : class
    {
        var messageJson = JsonSerializer.Serialize(message);

        try
        {
            PublishToRabbitMQ(messageJson, queueName);
            _logger.LogInformation($"✓ Mensaje publicado en cola '{queueName}'");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"⚠️  No se pudo enviar a RabbitMQ: {ex.Message}");
            _logger.LogInformation("💾 Guardando mensaje en tabla PendingMessages...");

            await SavePendingMessageAsync(messageJson, typeof(T).Name, queueName);
        }
    }

    private void PublishToRabbitMQ(string messageJson, string queueName)
    {
        IConnection? connection = null;
        try
        {
            connection = _connectionFactory.CreateConnection();

            if (!connection.IsOpen)
            {
                throw new Exception("La conexión a RabbitMQ no está abierta");
            }

            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueName == PaymentQueueConfig.QueueName
                    ? PaymentQueueConfig.DeclarationArguments
                    : null);

            var body = System.Text.Encoding.UTF8.GetBytes(messageJson);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogDebug($"Mensaje enviado a RabbitMQ - Cola: {queueName}, Tamaño: {body.Length} bytes");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error publicando a RabbitMQ: {ex.Message}", ex);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private async Task SavePendingMessageAsync(string messageJson, string eventType, string queueName)
    {
        try
        {
            var pendingMessage = new PendingMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                MessageBody = messageJson,
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false,
                RetryCount = 0
            };

            _context.PendingMessages.Add(pendingMessage);
            await _context.SaveChangesAsync();

            _logger.LogWarning(
                $"✓ Mensaje resguardado localmente - ID: {pendingMessage.Id} | " +
                $"Tipo: {eventType} | Cola destino: {queueName}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ CRÍTICO: No se pudo guardar mensaje pendiente: {ex.Message}");
            throw;
        }
    }
}
