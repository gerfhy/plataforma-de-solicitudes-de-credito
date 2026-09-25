namespace creditos.Services;

public interface IPieSocketService
{
    string? ClusterId { get; }
    string? ApiKey { get; }
    Task<bool> PublicarEstadoActualizadoAsync(string usuarioId, int solicitudId, string estado, string? motivoRechazo);
    string ObtenerCanalUsuario(string usuarioId);
}
