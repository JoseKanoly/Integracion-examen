using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscriptionPlatform.Api1.Subscriptions.Data;
using SubscriptionPlatform.Api1.Subscriptions.Services;
using SubscriptionPlatform.Shared.Events;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api1.Subscriptions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly SubscriptionContext _context;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        SubscriptionContext context,
        IMessagePublisher messagePublisher,
        ILogger<SubscriptionController> logger)
    {
        _context = context;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<UserSubscription>> GetUserSubscription(Guid userId)
    {
        var subscription = await _context.UserSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == userId);

        if (subscription == null)
        {
            return NotFound(new { message = "Suscripción no encontrada" });
        }

        subscription.PasswordHash = string.Empty;
        return Ok(subscription);
    }

    /// <summary>
    /// CRÍTICO: Procesa el pago. Si RabbitMQ falla, guarda localmente y retorna OK
    /// </summary>
    [HttpPost("process-payment")]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment(
        [FromBody] PaymentRequest request)
    {
        try
        {
            if (request == null || request.UserId == Guid.Empty)
            {
                return BadRequest(new { message = "UserId es requerido" });
            }

            if (request.Amount <= 0)
            {
                return BadRequest(new { message = "El monto debe ser mayor a cero" });
            }

            if (!IsValidPlanType(request.PlanType))
            {
                return BadRequest(new { message = "Plan no válido. Debe ser: Basic, Premium o Enterprise" });
            }

            var user = await _context.UserSubscriptions
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user == null)
            {
                user = new UserSubscription
                {
                    Id = request.UserId,
                    Email = "user@example.com",
                    FullName = "Usuario",
                    PlanType = request.PlanType,
                    SubscriptionDate = DateTime.UtcNow,
                    NextBillingDate = DateTime.UtcNow.AddMonths(1),
                    IsActive = true,
                    PaymentMethod = request.PaymentMethod ?? "Tarjeta de Crédito"
                };

                _context.UserSubscriptions.Add(user);
            }
            else
            {
                user.PlanType = request.PlanType;
                user.SubscriptionDate = DateTime.UtcNow;
                user.NextBillingDate = DateTime.UtcNow.AddMonths(1);
                user.IsActive = true;
                user.PaymentMethod = request.PaymentMethod ?? user.PaymentMethod;
                _context.UserSubscriptions.Update(user);
            }

            var paymentId = Guid.NewGuid();

            _context.PaymentHistory.Add(new PaymentHistoryEntry
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PaymentId = paymentId,
                Amount = request.Amount,
                PlanType = request.PlanType,
                PaymentMethod = request.PaymentMethod ?? "Tarjeta de Crédito",
                PaidAt = DateTime.UtcNow,
                Status = "Completado"
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation($"✓ Suscripción actualizada para usuario {request.UserId}");

            // PUBLICAR EVENTO CON RESILIENCIA
            var paymentEvent = new PaymentProcessedEvent
            {
                UserId = request.UserId,
                PaymentId = paymentId,
                Amount = request.Amount,
                PlanType = request.PlanType,
                ProcessedAt = DateTime.UtcNow,
                PaymentMethod = request.PaymentMethod ?? "Tarjeta de Crédito",
                Email = user.Email
            };

            await _messagePublisher.PublishAsync(
                paymentEvent,
                PaymentQueueConfig.QueueName);

            _logger.LogInformation(
                $"🎉 Pago procesado para usuario {request.UserId} | " +
                $"Plan: {request.PlanType} | Monto: ${request.Amount}");

            var safeSubscription = new UserSubscription
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PlanType = user.PlanType,
                SubscriptionDate = user.SubscriptionDate,
                NextBillingDate = user.NextBillingDate,
                IsActive = user.IsActive,
                PaymentMethod = user.PaymentMethod
            };

            return Ok(new PaymentResponse
            {
                Success = true,
                Message = "Pago procesado exitosamente. Los cambios se aplicarán en breve.",
                PaymentId = paymentEvent.PaymentId,
                Subscription = safeSubscription
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error procesando pago: {ex.Message}");
            return StatusCode(500, new
            {
                message = "Error procesando el pago",
                error = ex.Message
            });
        }
    }

    [HttpGet("user/{userId}/payment-history")]
    public async Task<ActionResult<List<PaymentHistoryEntry>>> GetPaymentHistory(Guid userId)
    {
        var history = await _context.PaymentHistory
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        return Ok(history);
    }

    [HttpGet("pending-messages")]
    public async Task<ActionResult> GetPendingMessages(bool? includeProcessed = false)
    {
        var query = _context.PendingMessages.AsQueryable();

        if (includeProcessed != true)
        {
            query = query.Where(m => !m.IsProcessed);
        }

        var pending = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return Ok(new
        {
            totalCount = pending.Count,
            unprocessedCount = pending.Count(p => !p.IsProcessed),
            messages = pending
        });
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var totalUsers = await _context.UserSubscriptions.CountAsync();
        var activeUsers = await _context.UserSubscriptions
            .Where(u => u.IsActive)
            .CountAsync();
        var pendingMessages = await _context.PendingMessages
            .Where(m => !m.IsProcessed)
            .CountAsync();

        var usersByPlan = await _context.UserSubscriptions
            .Where(u => u.IsActive)
            .GroupBy(u => u.PlanType)
            .Select(g => new { plan = g.Key, count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            totalUsers,
            activeUsers,
            pendingMessages,
            usersByPlan,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Publica un evento de pago INTENCIONALMENTE inválido (mensaje predefinido) para
    /// demostrar el flujo de rechazo automático a la Dead Letter Queue: API2 lo validará,
    /// fallará y lo enviará (BasicNack) a la DLQ, registrándolo en DeadLetterMessages.
    /// </summary>
    [HttpPost("send-invalid-event/{userId}")]
    public async Task<ActionResult> SendInvalidEvent(Guid userId)
    {
        // Mensaje predefinido inválido: monto negativo y plan inexistente.
        var invalidEvent = new PaymentProcessedEvent
        {
            UserId = userId == Guid.Empty ? Guid.NewGuid() : userId,
            PaymentId = Guid.NewGuid(),
            Amount = -1m,
            PlanType = "PlanInexistente",
            ProcessedAt = DateTime.UtcNow,
            PaymentMethod = "Tarjeta de Prueba",
            Email = "invalido@example.com"
        };

        await _messagePublisher.PublishAsync(invalidEvent, PaymentQueueConfig.QueueName);

        _logger.LogWarning(
            "Evento invalido de prueba publicado en la cola. " +
            "API2 debe rechazarlo automaticamente a la Dead Letter Queue.");

        return Ok(new
        {
            success = true,
            message = "Mensaje inválido enviado a la cola. API2 lo rechazará a la Dead Letter Queue.",
            payload = invalidEvent
        });
    }

    private bool IsValidPlanType(string planType)
    {
        return new[] { "Basic", "Premium", "Enterprise" }.Contains(planType);
    }
}
