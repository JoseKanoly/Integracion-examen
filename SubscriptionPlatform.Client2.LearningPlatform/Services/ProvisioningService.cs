using System.Net.Http.Json;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Client2.LearningPlatform.Services;

public interface IProvisioningService
{
    Task<UserAccessResponse?> GetUserAccessAsync(Guid userId);
    Task<List<CourseInfo>> GetUserCoursesAsync(Guid userId);
    Task<List<CourseInfo>> GetAllCoursesAsync();
    Task<object?> GetDLQMessagesAsync();
    Task<object?> GetStatsAsync();
}

public class ProvisioningService : IProvisioningService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProvisioningService> _logger;

    public ProvisioningService(HttpClient httpClient, ILogger<ProvisioningService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserAccessResponse?> GetUserAccessAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo acceso para usuario {userId}");

            var response = await _httpClient.GetAsync($"api/provisioning/user/{userId}/access");

            if (response.IsSuccessStatusCode)
            {
                var access = await response.Content.ReadFromJsonAsync<UserAccessResponse>();
                _logger.LogInformation(
                    $"✓ Acceso obtenido: Plan={access?.PlanType}, Cursos={access?.UnlockedCourses.Count}");
                return access;
            }

            _logger.LogWarning($"Error obteniendo acceso: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción obteniendo acceso: {ex.Message}");
            return null;
        }
    }

    public async Task<List<CourseInfo>> GetUserCoursesAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo cursos para usuario {userId}");

            var response = await _httpClient.GetAsync($"api/provisioning/user/{userId}/courses");

            if (response.IsSuccessStatusCode)
            {
                var courses = await response.Content.ReadFromJsonAsync<List<CourseInfo>>() ?? new();
                _logger.LogInformation(
                    $"✓ {courses.Count(c => c.IsUnlocked)}/{courses.Count} cursos desbloqueados");
                return courses;
            }

            _logger.LogWarning($"Error obteniendo cursos: {response.StatusCode}");
            return new List<CourseInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción obteniendo cursos: {ex.Message}");
            return new List<CourseInfo>();
        }
    }

    public async Task<List<CourseInfo>> GetAllCoursesAsync()
    {
        try
        {
            _logger.LogDebug("Obteniendo todos los cursos");

            var response = await _httpClient.GetAsync("api/provisioning/courses");

            if (response.IsSuccessStatusCode)
            {
                var courses = await response.Content.ReadFromJsonAsync<List<CourseInfo>>() ?? new();
                _logger.LogInformation($"✓ {courses.Count} cursos disponibles");
                return courses;
            }

            return new List<CourseInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción obteniendo cursos: {ex.Message}");
            return new List<CourseInfo>();
        }
    }

    public async Task<object?> GetDLQMessagesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/provisioning/dlq-messages");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<object>();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo DLQ: {ex.Message}");
            return null;
        }
    }

    public async Task<object?> GetStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/provisioning/stats");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<object>();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo estadísticas: {ex.Message}");
            return null;
        }
    }
}
