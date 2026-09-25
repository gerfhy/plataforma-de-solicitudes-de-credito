using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using creditos.Data;
using creditos.Models;

namespace creditos.Services;

public class SolicitudCacheService : ISolicitudCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SolicitudCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SolicitudCacheService(
        IDistributedCache cache, 
        IServiceProvider serviceProvider,
        ILogger<SolicitudCacheService> logger)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private static string GenerarClaveCache(string usuarioId) => $"solicitudes_usuario_{usuarioId}";

    public async Task<List<SolicitudCredito>?> ObtenerSolicitudesCacheAsync(string usuarioId)
    {
        try
        {
            var key = GenerarClaveCache(usuarioId);
            var cachedJson = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(cachedJson))
            {
                return null;
            }

            _logger.LogInformation("Caché Redis HIT: listado de solicitudes para usuario {UsuarioId}", usuarioId);
            return JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al leer desde la caché distribuida para el usuario {UsuarioId}", usuarioId);
            return null;
        }
    }

    public async Task GuardarSolicitudesCacheAsync(string usuarioId, List<SolicitudCredito> solicitudes)
    {
        try
        {
            var key = GenerarClaveCache(usuarioId);
            var serialized = JsonSerializer.Serialize(solicitudes, JsonOptions);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) // Requerido: 60s
            };

            await _cache.SetStringAsync(key, serialized, options);
            _logger.LogInformation("Caché Redis SET: listado cacheado por 60s para usuario {UsuarioId}", usuarioId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al escribir en la caché distribuida para el usuario {UsuarioId}", usuarioId);
        }
    }

    public async Task InvalidarCacheUsuarioAsync(string usuarioId)
    {
        try
        {
            var key = GenerarClaveCache(usuarioId);
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Caché Redis INVALIDADA para usuario {UsuarioId}", usuarioId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar la caché distribuida para el usuario {UsuarioId}", usuarioId);
        }
    }

    public async Task InvalidarCacheClienteAsync(int clienteId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cliente = await context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteId);

            if (cliente != null && !string.IsNullOrEmpty(cliente.UsuarioId))
            {
                await InvalidarCacheUsuarioAsync(cliente.UsuarioId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar la caché para el clienteId {ClienteId}", clienteId);
        }
    }
}
