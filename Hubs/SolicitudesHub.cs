using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace creditos.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    private readonly ILogger<SolicitudesHub> _logger;

    public SolicitudesHub(ILogger<SolicitudesHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var usuarioId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("WebSocket Hub conectado: Usuario {UsuarioId} en Conexión {ConnectionId}", usuarioId, connectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var usuarioId = Context.UserIdentifier;
        _logger.LogInformation("WebSocket Hub desconectado: Usuario {UsuarioId}", usuarioId);
        await base.OnDisconnectedAsync(exception);
    }
}
