namespace SubscriptionPlatform.Shared.Events;

public class PaymentProcessedEvent
{
    public Guid UserId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
