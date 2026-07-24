using System.Net.Http.Json;
using SubscriptionPlatform.Shared.Models;

namespace SubscriptionPlatform.Client1.UserPortal.Services;

public interface ISubscriptionService
{
    Task<UserSubscription?> GetUserSubscriptionAsync(Guid userId);
    Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
    Task<List<PaymentHistoryEntry>> GetPaymentHistoryAsync(Guid userId);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<object?> GetPendingMessagesAsync();
    Task<object?> GetStatsAsync();
    Task<PaymentResponse> SendInvalidTestEventAsync(Guid userId);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(HttpClient httpClient, ILogger<SubscriptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserSubscription?> GetUserSubscriptionAsync(Guid userId)
    {
        try
        {
            _logger.LogInformation($"Obteniendo suscripción para usuario {userId}");

            var response = await _httpClient.GetAsync($"api/subscription/user/{userId}");

            if (response.IsSuccessStatusCode)
            {
                var subscription = await response.Content.ReadFromJsonAsync<UserSubscription>();
                _logger.LogInformation($"✓ Suscripción obtenida: {subscription?.PlanType}");
                return subscription;
            }

            _logger.LogWarning($"Error obteniendo suscripción: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción obteniendo suscripción: {ex.Message}");
            return null;
        }
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        try
        {
            _logger.LogInformation(
                $"Procesando pago para usuario {request.UserId} | Plan: {request.PlanType}");

            var response = await _httpClient.PostAsJsonAsync(
                "api/subscription/process-payment",
                request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PaymentResponse>();
                _logger.LogInformation("✓ Pago procesado exitosamente");
                return result ?? new PaymentResponse { Success = false, Message = "Respuesta vacía" };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning($"Error procesando pago: {response.StatusCode} | Detalle: {errorContent}");

            return new PaymentResponse
            {
                Success = false,
                Message = $"Error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción procesando pago: {ex.Message}");
            return new PaymentResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<List<PaymentHistoryEntry>> GetPaymentHistoryAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/subscription/user/{userId}/payment-history");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<PaymentHistoryEntry>>() ?? new();
            }

            _logger.LogWarning($"Error obteniendo historial: {response.StatusCode}");
            return new List<PaymentHistoryEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción obteniendo historial: {ex.Message}");
            return new List<PaymentHistoryEntry>();
        }
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return result ?? new AuthResponse { Success = false, Message = "Respuesta vacía del servidor" };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción en registro: {ex.Message}");
            return new AuthResponse { Success = false, Message = "No se pudo conectar con el servidor" };
        }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return result ?? new AuthResponse { Success = false, Message = "Respuesta vacía del servidor" };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción en login: {ex.Message}");
            return new AuthResponse { Success = false, Message = "No se pudo conectar con el servidor" };
        }
    }

    public async Task<object?> GetPendingMessagesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/subscription/pending-messages");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<object>();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo mensajes pendientes: {ex.Message}");
            return null;
        }
    }

    public async Task<object?> GetStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/subscription/stats");

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

    public async Task<PaymentResponse> SendInvalidTestEventAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"api/subscription/send-invalid-event/{userId}", null);

            if (response.IsSuccessStatusCode)
            {
                return new PaymentResponse
                {
                    Success = true,
                    Message = "Mensaje inválido enviado a la cola."
                };
            }

            _logger.LogWarning($"Error enviando evento inválido: {response.StatusCode}");
            return new PaymentResponse { Success = false, Message = $"Error: {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Excepción enviando evento inválido: {ex.Message}");
            return new PaymentResponse { Success = false, Message = ex.Message };
        }
    }
}
