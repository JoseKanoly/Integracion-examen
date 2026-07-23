namespace SubscriptionPlatform.Shared.Events;

/// <summary>
/// Argumentos de declaración de la cola "payment-processed", compartidos entre publicador (API 1) y
/// consumidor (API 2). RabbitMQ exige que todo `queue.declare` sobre una cola existente use argumentos
/// idénticos a los usados en su primera declaración; de lo contrario rechaza el canal con
/// PRECONDITION_FAILED. Ambos lados deben construir la cola con esta misma definición.
/// </summary>
public static class PaymentQueueConfig
{
    public const string QueueName = "payment-processed";
    public const string DeadLetterExchange = "dlx-payment";
    public const string DeadLetterQueueName = "dlq-payment-processed";

    public static Dictionary<string, object> DeclarationArguments => new()
    {
        { "x-dead-letter-exchange", DeadLetterExchange },
        { "x-dead-letter-routing-key", DeadLetterQueueName },
        { "x-message-ttl", 3600000 }
    };
}
