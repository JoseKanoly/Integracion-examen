using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscriptionPlatform.Api1.Subscriptions.Data;
using SubscriptionPlatform.Api1.Subscriptions.Services;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api1.Subscriptions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SubscriptionContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(SubscriptionContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Email inválido" });
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "El nombre es requerido" });
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new AuthResponse { Success = false, Message = "La contraseña debe tener al menos 6 caracteres" });
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _context.UserSubscriptions.AnyAsync(u => u.Email == email);
        if (exists)
        {
            return Conflict(new AuthResponse { Success = false, Message = "Ya existe una cuenta con ese email" });
        }

        var user = new UserSubscription
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            PlanType = "Basic",
            SubscriptionDate = DateTime.UtcNow,
            IsActive = false,
            PaymentMethod = "Sin configurar",
            PasswordHash = PasswordHasher.Hash(request.Password)
        };

        _context.UserSubscriptions.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"✓ Cuenta registrada: {email} ({user.Id})");

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Cuenta creada exitosamente",
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PlanType = user.PlanType
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";

        var user = await _context.UserSubscriptions.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) ||
            !PasswordHasher.Verify(request.Password ?? "", user.PasswordHash))
        {
            _logger.LogWarning($"Intento de login fallido para {email}");
            return Unauthorized(new AuthResponse { Success = false, Message = "Email o contraseña incorrectos" });
        }

        _logger.LogInformation($"✓ Login exitoso: {email}");

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Bienvenido",
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PlanType = user.PlanType
        });
    }
}
