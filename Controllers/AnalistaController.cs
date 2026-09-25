using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Hubs;
using creditos.Models;
using creditos.Services;

namespace creditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISolicitudCacheService _cacheService;
    private readonly IHubContext<SolicitudesHub> _hubContext;
    private readonly IPieSocketService _pieSocketService;
    private readonly ILogger<AnalistaController> _logger;

    public AnalistaController(
        ApplicationDbContext context, 
        ISolicitudCacheService cacheService,
        IHubContext<SolicitudesHub> hubContext,
        IPieSocketService pieSocketService,
        ILogger<AnalistaController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _hubContext = hubContext;
        _pieSocketService = pieSocketService;
        _logger = logger;
    }

    // GET: /Analista
    public async Task<IActionResult> Index()
    {
        var solicitudesPendientes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        var viewModel = new PanelAnalistaViewModel
        {
            SolicitudesPendientes = solicitudesPendientes
        };

        ViewData["Title"] = "Panel de Analista de Riesgo";
        ViewData["KpiTotalCredito"] = $"${viewModel.MontoTotalPendiente:N0}";
        ViewData["KpiTotalBadge"] = $"{viewModel.SolicitudesViables} Viables";
        ViewData["KpiPendientes"] = viewModel.TotalPendientes.ToString();
        ViewData["KpiPendientesBadge"] = $"{viewModel.SolicitudesExcedidas} en Riesgo";
        ViewData["KpiLabel1"] = "Monto por Evaluar";
        ViewData["KpiLabel2"] = "Solicitudes Pendientes";

        return View(viewModel);
    }

    // POST: /Analista/Aprobar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["ErrorMessage"] = $"No se encontró la solicitud #{id}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 1: No procesar solicitudes ya aprobadas o rechazadas
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = $"La solicitud #{solicitud.Id} ya fue procesada anteriormente y está en estado {solicitud.Estado}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 2: No aprobar si el monto excede 5 veces los ingresos
        var ingresos = solicitud.Cliente?.IngresosMensuales ?? 0m;
        var limiteAprobacion = ingresos * 5;

        if (solicitud.MontoSolicitado > limiteAprobacion)
        {
            TempData["ErrorMessage"] = $"No se puede aprobar la solicitud #{solicitud.Id}: El monto solicitado (${solicitud.MontoSolicitado:N2}) supera el límite de 5 veces los ingresos del cliente (${limiteAprobacion:N2}).";
            _logger.LogWarning("Intento de aprobar solicitud #{SolicitudId} que excede 5x ingresos (${Monto} > ${Limite})", solicitud.Id, solicitud.MontoSolicitado, limiteAprobacion);
            return RedirectToAction(nameof(Index));
        }

        // 1. Guardar primero el estado en la base de datos
        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;

        // Registrar notificación persistente para el cliente
        if (!string.IsNullOrEmpty(solicitud.Cliente?.UsuarioId))
        {
            var notificacionAprobado = new Notificacion
            {
                MessageId = Guid.NewGuid().ToString(),
                SolicitudId = solicitud.Id,
                UsuarioId = solicitud.Cliente.UsuarioId,
                Texto = $"¡Felicitaciones! Tu solicitud de crédito #{solicitud.Id} ha sido aprobada por ${solicitud.MontoSolicitado:N2}.",
                FechaProcesamientoUtc = DateTime.UtcNow
            };
            _context.Notificaciones.Add(notificacionAprobado);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Solicitud #{SolicitudId} APROBADA exitosamente por Analista", solicitud.Id);

        // 2. Invalidar Caché Redis del cliente afectado (Pregunta 4)
        await _cacheService.InvalidarCacheClienteAsync(solicitud.ClienteId);

        // 3. Emitir evento WebSocket SolicitudEstadoActualizado únicamente al usuario propietario (Pregunta 6)
        if (!string.IsNullOrEmpty(solicitud.Cliente?.UsuarioId))
        {
            // Emisión nativa por Hub
            await _hubContext.Clients.User(solicitud.Cliente.UsuarioId).SendAsync("SolicitudEstadoActualizado", new
            {
                solicitudId = solicitud.Id,
                estado = "Aprobado",
                motivoRechazo = (string?)null
            });

            // Emisión por PieSocket (piehost.com)
            await _pieSocketService.PublicarEstadoActualizadoAsync(
                solicitud.Cliente.UsuarioId,
                solicitud.Id,
                "Aprobado",
                null);

            _logger.LogInformation("WebSocket SolicitudEstadoActualizado (Aprobado) emitido a usuario {UsuarioId} para solicitud #{SolicitudId}", solicitud.Cliente.UsuarioId, solicitud.Id);
        }

        TempData["SuccessMessage"] = $"¡Solicitud #{solicitud.Id} aprobada con éxito por un monto de ${solicitud.MontoSolicitado:N2}!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Analista/Rechazar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["ErrorMessage"] = $"No se encontró la solicitud #{id}.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 1: No procesar solicitudes ya aprobadas o rechazadas
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = $"La solicitud #{solicitud.Id} ya fue procesada anteriormente.";
            return RedirectToAction(nameof(Index));
        }

        // Validación 2: Motivo obligatorio en rechazo
        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["ErrorMessage"] = "El motivo de rechazo es obligatorio para denegar la solicitud de crédito.";
            return RedirectToAction(nameof(Index));
        }

        // 1. Guardar primero el estado en la base de datos
        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();

        // Registrar notificación persistente para el cliente con motivo de rechazo
        if (!string.IsNullOrEmpty(solicitud.Cliente?.UsuarioId))
        {
            var notificacionRechazo = new Notificacion
            {
                MessageId = Guid.NewGuid().ToString(),
                SolicitudId = solicitud.Id,
                UsuarioId = solicitud.Cliente.UsuarioId,
                Texto = $"Tu solicitud de crédito #{solicitud.Id} ha sido rechazada. Motivo: {solicitud.MotivoRechazo}",
                FechaProcesamientoUtc = DateTime.UtcNow
            };
            _context.Notificaciones.Add(notificacionRechazo);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Solicitud #{SolicitudId} RECHAZADA por Analista. Motivo: {Motivo}", solicitud.Id, solicitud.MotivoRechazo);

        // 2. Invalidar Caché Redis del cliente afectado (Pregunta 4)
        await _cacheService.InvalidarCacheClienteAsync(solicitud.ClienteId);

        // 3. Emitir evento WebSocket SolicitudEstadoActualizado únicamente al usuario propietario (Pregunta 6)
        if (!string.IsNullOrEmpty(solicitud.Cliente?.UsuarioId))
        {
            // Emisión nativa por Hub
            await _hubContext.Clients.User(solicitud.Cliente.UsuarioId).SendAsync("SolicitudEstadoActualizado", new
            {
                solicitudId = solicitud.Id,
                estado = "Rechazado",
                motivoRechazo = solicitud.MotivoRechazo
            });

            // Emisión por PieSocket (piehost.com)
            await _pieSocketService.PublicarEstadoActualizadoAsync(
                solicitud.Cliente.UsuarioId,
                solicitud.Id,
                "Rechazado",
                solicitud.MotivoRechazo);

            _logger.LogInformation("WebSocket SolicitudEstadoActualizado (Rechazado) emitido a usuario {UsuarioId} para solicitud #{SolicitudId}", solicitud.Cliente.UsuarioId, solicitud.Id);
        }

        TempData["SuccessMessage"] = $"Solicitud #{solicitud.Id} rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
