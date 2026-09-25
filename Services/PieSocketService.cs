using System.Text;
using System.Text.Json;

namespace creditos.Services;

public class PieSocketService : IPieSocketService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PieSocketService> _logger;

    public string? ClusterId => _configuration["PieSocket:ClusterId"] ?? Environment.GetEnvironmentVariable("PIESOCKET_CLUSTER_ID") ?? "free.blr2";
    public string? ApiKey => _configuration["PieSocket:ApiKey"] ?? Environment.GetEnvironmentVariable("PIESOCKET_API_KEY");
    public string? ApiSecret => _configuration["PieSocket:ApiSecret"] ?? Environment.GetEnvironmentVariable("PIESOCKET_API_SECRET");

    public PieSocketService(IConfiguration configuration, HttpClient httpClient, ILogger<PieSocketService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string ObtenerCanalUsuario(string usuarioId)
    {
        // Identificador de sala única por usuario para garantizar aislamiento estricto
        return $"solicitud_usuario_{usuarioId.Replace("-", "")}";
    }

    public async Task<bool> PublicarEstadoActualizadoAsync(string usuarioId, int solicitudId, string estado, string? motivoRechazo)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(ApiSecret))
        {
            _logger.LogWarning("PieSocket no está configurado (ApiKey o ApiSecret ausentes).");
            return false;
        }

        var roomId = ObtenerCanalUsuario(usuarioId);
        var url = $"https://{ClusterId}.piesocket.com/api/v4/publish";

        var payload = new
        {
            key = ApiKey,
            secret = ApiSecret,
            roomId = roomId,
            message = new
            {
                @event = "SolicitudEstadoActualizado",
                data = new
                {
                    solicitudId = solicitudId,
                    estado = estado,
                    motivoRechazo = motivoRechazo
                }
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Evento PieSocket publicado con éxito a canal {RoomId} para solicitud #{SolicitudId}", roomId, solicitudId);
                return true;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error al publicar evento en PieSocket: HTTP {StatusCode} - {Error}", response.StatusCode, errorBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al emitir mensaje a PieSocket");
            return false;
        }
    }
}
