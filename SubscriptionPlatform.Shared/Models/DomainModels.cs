namespace SubscriptionPlatform.Shared.Models;

/// <summary>
/// Modelo de suscripción del usuario en API 1
/// </summary>
public class UserSubscription
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public DateTime SubscriptionDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public bool IsActive { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

/// <summary>
/// Historial de pagos del usuario (API 1) - cada cobro exitoso queda registrado aquí
/// </summary>
public class PaymentHistoryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public string Status { get; set; } = "Completado";
}

/// <summary>
/// CRÍTICA PARA RESILIENCIA: Almacena mensajes cuando RabbitMQ no está disponible
/// </summary>
public class PendingMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; } = 0;
}

/// <summary>
/// Acceso del usuario en la plataforma de aprendizaje (API 2)
/// </summary>
public class UserAccess
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public DateTime ActivatedAt { get; set; }
    public bool HasPremiumAccess { get; set; }
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Permisos de acceso a cursos específicos (API 2)
/// </summary>
public class CoursePermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public DateTime UnlockedAt { get; set; }
}

/// <summary>
/// AUDITORÍA: Mensajes rechazados que van a Dead Letter Queue
/// </summary>
public class DeadLetterMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string ErrorReason { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}

/// <summary>
/// DTOs para APIs
/// </summary>
public class PaymentRequest
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
}

public class PaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public UserSubscription? Subscription { get; set; }
}

public class UserAccessResponse
{
    public Guid UserId { get; set; }
    public string PlanType { get; set; } = string.Empty;
    public bool HasPremiumAccess { get; set; }
    public DateTime ActivatedAt { get; set; }
    public List<string> UnlockedCourses { get; set; } = new();
}

public class CourseInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PlanRequired { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
}

/// <summary>
/// DTOs de autenticación (registro e inicio de sesión contra API 1)
/// </summary>
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
}
