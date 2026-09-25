using System.Text;
using System.Text.Json;
using creditos.Models;
using RabbitMQ.Client;

namespace creditos.Services;

public class RabbitMqPublisherService : IRabbitMqPublisherService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisherService> _logger;

    public RabbitMqPublisherService(IConfiguration configuration, ILogger<RabbitMqPublisherService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string? ObtenerConnectionString()
    {
        return _configuration["RabbitMq:ConnectionString"]
            ?? _configuration["RabbitMq__ConnectionString"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConnectionString")
            ?? Environment.GetEnvironmentVariable("RABBITMQ_URL");
    }

    private string ObtenerNombreCola()
    {
        return _configuration["RabbitMq:QueueName"]
            ?? _configuration["RabbitMq__QueueName"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__QueueName")
            ?? "solicitudes.notificaciones";
    }

    public async Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistradaMensaje mensaje)
    {
        var connectionString = ObtenerConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("No se configuró la cadena de conexión a RabbitMQ/CloudAMQP (RabbitMq:ConnectionString).");
            return false;
        }

        var queueName = ObtenerNombreCola();

        return await Task.Run(() =>
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(connectionString),
                    DispatchConsumersAsync = true,
                    AutomaticRecoveryEnabled = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                // 1. Declarar cola durable requerida por el examen
                channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // 2. Activar Confirmación del Publicador (Publisher Confirms)
                channel.ConfirmSelect();

                // 3. Crear mensaje persistente con MessageId (UUID)
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Type = "SolicitudRegistrada";
                properties.MessageId = mensaje.MessageId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                var json = JsonSerializer.Serialize(mensaje);
                var body = Encoding.UTF8.GetBytes(json);

                // 4. Publicar en el broker
                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);

                // 5. Esperar confirmación del broker (evita pérdidas ante fallos de red)
                channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

                _logger.LogInformation(
                    "Cloud MQ: Mensaje SolicitudRegistrada publicado exitosamente a CloudAMQP (Queue: {Queue}, MessageId: {MessageId}, SolicitudId: {SolicitudId})",
                    queueName, mensaje.MessageId, mensaje.SolicitudId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Cloud MQ: Fallo crítico al publicar en CloudAMQP con MessageId: {MessageId} para SolicitudId: {SolicitudId}. La solicitud se conserva en BD.",
                    mensaje.MessageId, mensaje.SolicitudId);
                return false;
            }
        });
    }
}
