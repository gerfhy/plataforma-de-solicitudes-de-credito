using creditos.Models;

namespace creditos.Services;

public interface ISolicitudCacheService
{
    Task<List<SolicitudCredito>?> ObtenerSolicitudesCacheAsync(string usuarioId);
    Task GuardarSolicitudesCacheAsync(string usuarioId, List<SolicitudCredito> solicitudes);
    Task InvalidarCacheUsuarioAsync(string usuarioId);
    Task InvalidarCacheClienteAsync(int clienteId);
}
