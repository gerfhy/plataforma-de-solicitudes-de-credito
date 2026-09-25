using System.Text;
using System.Text.Json;
using creditos.Data;
using creditos.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace creditos.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqConsumerService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RabbitMqConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    private bool IsConsumerEnabled()
    {
        var val = _configuration["RabbitMq:ConsumerEnabled"]
            ?? _configuration["RabbitMq__ConsumerEnabled"]
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConsumerEnabled")
            ?? "true";

        return bool.TryParse(val, out var enabled) && enabled;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsConsumerEnabled())
        {
            _logger.LogWarning("Cloud MQ: El consumidor RabbitMQ está DESACTIVADO (RabbitMq:ConsumerEnabled=false). Los mensajes se acumularán en CloudAMQP.");
            return;
        }

        var connectionString = ObtenerConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("Cloud MQ: No se encontró cadena de conexión para el consumidor (RabbitMq:ConnectionString).");
            return;
        }

        var queueName = ObtenerNombreCola();

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declarar la cola durable (garantiza idempotencia con el productor)
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Calidad de servicio: consumir 1 mensaje a la vez con ACK manual
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var deliveryTag = ea.DeliveryTag;
                var rawBody = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation("Cloud MQ Consumidor: Mensaje recibido en cola {Queue}, Tag: {Tag}", queueName, deliveryTag);

                SolicitudRegistradaMensaje? mensaje = null;
                try
                {
                    mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMensaje>(rawBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cloud MQ Consumidor: Mensaje inválido detectado. Rechazando sin reencolar. Body: {Body}", rawBody);
                    // Mensaje inválido se rechaza sin reencolar para evitar bucles infinitos
                    _channel.BasicReject(deliveryTag, requeue: false);
                    return;
                }

                if (mensaje == null || string.IsNullOrWhiteSpace(mensaje.MessageId) || mensaje.SolicitudId <= 0 || string.IsNullOrWhiteSpace(mensaje.UsuarioId))
                {
                    _logger.LogError("Cloud MQ Consumidor: Mensaje incompleto o datos corruptos. Rechazando sin reencolar. MessageId: {MessageId}", mensaje?.MessageId);
                    _channel.BasicReject(deliveryTag, requeue: false);
                    return;
                }

                // Procesamiento transaccional con SQLite
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // 1. Control de duplicados por MessageId (Deduplicación)
                    var yaExiste = await dbContext.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId, stoppingToken);
                    if (yaExiste)
                    {
                        _logger.LogWarning("Cloud MQ Consumidor: Mensaje duplicado recibido (MessageId: {MessageId}). Confirmando con ACK manual sin insertar nuevamente.", mensaje.MessageId);
                        _channel.BasicAck(deliveryTag, multiple: false);
                        return;
                    }

                    // 2. Guardar en SQLite la notificación
                    var notificacion = new Notificacion
                    {
                        MessageId = mensaje.MessageId,
                        SolicitudId = mensaje.SolicitudId,
                        UsuarioId = mensaje.UsuarioId,
                        Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                        FechaProcesamientoUtc = DateTime.UtcNow
                    };

                    dbContext.Notificaciones.Add(notificacion);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation(
                        "Cloud MQ Consumidor: Notificación #{NotifId} guardada en BD para usuario {UsuarioId}, Solicitud #{SolicitudId}, MessageId: {MessageId}",
                        notificacion.Id, notificacion.UsuarioId, notificacion.SolicitudId, notificacion.MessageId);

                    // 3. Confirmar con ACK manual ÚNICAMENTE después de guardar la notificación
                    _channel.BasicAck(deliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Cloud MQ Consumidor: Error al procesar en BD mensaje con MessageId: {MessageId}. No se confirma como exitoso.",
                        mensaje.MessageId);

                    // No confirmar como exitoso, rechazar sin reencolar o dejar en cola según política de control de reintentos
                    _channel.BasicNack(deliveryTag, multiple: false, requeue: false);
                }
            };

            // Iniciar consumo con ACK manual (autoAck: false)
            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Cloud MQ Consumidor: Iniciado y escuchando en cola {Queue} con ACK manual activado.", queueName);

            // Mantener el servicio en ejecución hasta que se solicite la detención
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cloud MQ Consumidor: Detención solicitada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud MQ Consumidor: Excepción no controlada en el servicio consumidor.");
        }
    }

    public override void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
        catch
        {
            // Ignorar errores al cerrar
        }
        base.Dispose();
    }
}
