using RabbitMQ.Client;
using SubscriptionPlatform.Shared.Events;

namespace SubscriptionPlatform.Api2.Provisioning.RabbitMQ;

/// <summary>
/// CRÍTICO: Configura Dead Letter Queue para rechazar mensajes inválidos
///
/// ARQUITECTURA:
/// payment-processed (Cola Principal)
///     ↓ (Si BasicNack)
/// dlx-payment (Dead Letter Exchange)
///     ↓
/// dlq-payment-processed (Dead Letter Queue)
/// </summary>
public static class RabbitMQSetup
{
    public static void ConfigureQueues(IConnection connection, ILogger logger)
    {
        if (connection == null || !connection.IsOpen)
        {
            logger.LogWarning("⚠️  RabbitMQ no está disponible para configurar colas");
            return;
        }

        try
        {
            using var channel = connection.CreateModel();

            logger.LogInformation("🔧 Configurando RabbitMQ con Dead Letter Queue...");

            // COLA PRINCIPAL
            channel.QueueDeclare(
                queue: PaymentQueueConfig.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: PaymentQueueConfig.DeclarationArguments);

            logger.LogInformation($"✓ Cola principal '{PaymentQueueConfig.QueueName}' configurada");

            // DEAD LETTER EXCHANGE
            channel.ExchangeDeclare(
                exchange: PaymentQueueConfig.DeadLetterExchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            logger.LogInformation($"✓ Dead Letter Exchange '{PaymentQueueConfig.DeadLetterExchange}' configurado");

            // DEAD LETTER QUEUE
            channel.QueueDeclare(
                queue: PaymentQueueConfig.DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            logger.LogInformation($"✓ Dead Letter Queue '{PaymentQueueConfig.DeadLetterQueueName}' configurada");

            // BINDING
            channel.QueueBind(
                queue: PaymentQueueConfig.DeadLetterQueueName,
                exchange: PaymentQueueConfig.DeadLetterExchange,
                routingKey: PaymentQueueConfig.DeadLetterQueueName);

            logger.LogInformation("✓ Binding DLX -> DLQ establecido");

            logger.LogInformation(
                "✅ RabbitMQ configurado correctamente\n" +
                "   - Cola Principal: payment-processed\n" +
                "   - Dead Letter Exchange: dlx-payment\n" +
                "   - Dead Letter Queue: dlq-payment-processed");
        }
        catch (Exception ex)
        {
            logger.LogError($"❌ Error configurando RabbitMQ: {ex.Message}");
            throw;
        }
    }
}
