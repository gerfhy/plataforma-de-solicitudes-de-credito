using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Models;
using creditos.Services;

namespace creditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISolicitudCacheService _cacheService;
    private readonly ILogger<AnalistaController> _logger;

    public AnalistaController(
        ApplicationDbContext context, 
        ISolicitudCacheService cacheService,
        ILogger<AnalistaController> logger)
    {
        _context = context;
        _cacheService = cacheService;
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

        // Actualizar estado a Aprobado
        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Solicitud #{SolicitudId} APROBADA exitosamente por Analista", solicitud.Id);

        // Invalidar Caché Redis del cliente afectado (Pregunta 4)
        await _cacheService.InvalidarCacheClienteAsync(solicitud.ClienteId);

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

        // Actualizar estado a Rechazado con motivo
        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Solicitud #{SolicitudId} RECHAZADA por Analista. Motivo: {Motivo}", solicitud.Id, solicitud.MotivoRechazo);

        // Invalidar Caché Redis del cliente afectado (Pregunta 4)
        await _cacheService.InvalidarCacheClienteAsync(solicitud.ClienteId);

        TempData["SuccessMessage"] = $"Solicitud #{solicitud.Id} rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
