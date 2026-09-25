using creditos.Models;

namespace creditos.Services;

public interface IRabbitMqPublisherService
{
    Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMensaje mensaje);
}
