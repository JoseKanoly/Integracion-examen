using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscriptionPlatform.Api2.Provisioning.Data;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Api2.Provisioning.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvisioningController : ControllerBase
{
    private readonly ProvisioningContext _context;
    private readonly ILogger<ProvisioningController> _logger;

    public ProvisioningController(
        ProvisioningContext context,
        ILogger<ProvisioningController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("user/{userId}/access")]
    public async Task<ActionResult<UserAccessResponse>> GetUserAccess(Guid userId)
    {
        _logger.LogDebug($"Consultando acceso para usuario {userId}");

        var userAccess = await _context.UserAccesses
            .FirstOrDefaultAsync(ua => ua.UserId == userId);

        if (userAccess == null)
        {
            return NotFound(new
            {
                message = "Usuario no tiene acceso configurado",
                userId
            });
        }

        var unlockedCourses = await _context.CoursePermissions
            .Where(cp => cp.UserId == userId && cp.IsUnlocked)
            .Select(cp => cp.CourseId)
            .ToListAsync();

        var response = new UserAccessResponse
        {
            UserId = userId,
            PlanType = userAccess.PlanType,
            HasPremiumAccess = userAccess.HasPremiumAccess,
            ActivatedAt = userAccess.ActivatedAt,
            UnlockedCourses = unlockedCourses
        };

        _logger.LogInformation(
            $"✓ Acceso obtenido para usuario {userId} | " +
            $"Plan: {userAccess.PlanType} | " +
            $"Cursos desbloqueados: {unlockedCourses.Count}");

        return Ok(response);
    }

    [HttpGet("courses")]
    public ActionResult<List<CourseInfo>> GetAllCourses()
    {
        var courses = GetAllCoursesData()
            .Select(c => new CourseInfo
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                PlanRequired = c.PlanRequired
            })
            .ToList();

        return Ok(courses);
    }

    [HttpGet("user/{userId}/courses")]
    public async Task<ActionResult<List<CourseInfo>>> GetUserCourses(Guid userId)
    {
        _logger.LogDebug($"Obteniendo cursos para usuario {userId}");

        var userAccess = await _context.UserAccesses
            .FirstOrDefaultAsync(ua => ua.UserId == userId);

        if (userAccess == null)
        {
            return NotFound(new { message = "Usuario no tiene acceso" });
        }

        var unlockedCourseIds = await _context.CoursePermissions
            .Where(cp => cp.UserId == userId && cp.IsUnlocked)
            .Select(cp => cp.CourseId)
            .ToListAsync();

        var allCourses = GetAllCoursesData();
        var coursesWithAccess = allCourses.Select(course => new CourseInfo
        {
            Id = course.Id,
            Name = course.Name,
            Description = course.Description,
            PlanRequired = course.PlanRequired,
            IsUnlocked = unlockedCourseIds.Contains(course.Id)
        }).ToList();

        _logger.LogInformation(
            $"✓ {unlockedCourseIds.Count}/{allCourses.Count} cursos desbloqueados para usuario {userId}");

        return Ok(coursesWithAccess);
    }

    [HttpGet("dlq-messages")]
    public async Task<ActionResult> GetDLQMessages(int limit = 50)
    {
        var dlqMessages = await _context.DeadLetterMessages
            .OrderByDescending(m => m.ReceivedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(new
        {
            totalCount = dlqMessages.Count,
            messages = dlqMessages.Select(m => new
            {
                m.Id,
                m.EventType,
                m.ErrorReason,
                m.ReceivedAt,
                messagePreview = m.MessageBody.Length > 100
                    ? m.MessageBody.Substring(0, 100) + "..."
                    : m.MessageBody
            })
        });
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var totalUsers = await _context.UserAccesses.CountAsync();
        var premiumUsers = await _context.UserAccesses
            .Where(ua => ua.HasPremiumAccess)
            .CountAsync();
        var totalCourseAccesses = await _context.CoursePermissions.CountAsync();
        var dlqMessageCount = await _context.DeadLetterMessages.CountAsync();

        var usersByPlan = await _context.UserAccesses
            .GroupBy(ua => ua.PlanType)
            .Select(g => new
            {
                plan = g.Key,
                count = g.Count()
            })
            .ToListAsync();

        return Ok(new
        {
            totalUsers,
            premiumUsers,
            totalCourseAccesses,
            dlqMessageCount,
            usersByPlan,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "API 2 - Aprovisionamiento"
        });
    }

    private List<(string Id, string Name, string Description, string PlanRequired)> GetAllCoursesData()
    {
        return new List<(string, string, string, string)>
        {
            ("course-001-csharp-basics", "C# Fundamentos", "Aprende los conceptos básicos de C#", "Basic"),
            ("course-002-dotnet-intro", ".NET Introducción", "Primeros pasos con .NET Framework", "Basic"),
            ("course-003-entity-framework", "Entity Framework Core", "Acceso a datos con EF Core", "Premium"),
            ("course-004-asp-net-core", "ASP.NET Core", "Desarrollo web con ASP.NET Core", "Premium"),
            ("course-005-microservices", "Microservicios", "Arquitectura de microservicios", "Enterprise"),
            ("course-006-cloud-deployment", "Cloud y DevOps", "Despliegue en la nube", "Enterprise")
        };
    }
}
