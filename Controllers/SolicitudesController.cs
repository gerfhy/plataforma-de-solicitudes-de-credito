using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Models;

namespace creditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Solicitudes (Mis Solicitudes con filtros)
    public async Task<IActionResult> Index(CatalogoSolicitudesViewModel filter)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        // Obtener o registrar cliente asociado al usuario
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user.Id);
        if (cliente == null)
        {
            cliente = new Cliente
            {
                UsuarioId = user.Id,
                IngresosMensuales = 3000.00m,
                Activo = true
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
        }

        // Validaciones Server-Side requeridas por Pregunta 2:
        // 1. No aceptar montos negativos
        if (filter.MontoMin.HasValue && filter.MontoMin.Value < 0)
        {
            ModelState.AddModelError(nameof(filter.MontoMin), "El monto mínimo no puede ser un valor negativo.");
        }

        if (filter.MontoMax.HasValue && filter.MontoMax.Value < 0)
        {
            ModelState.AddModelError(nameof(filter.MontoMax), "El monto máximo no puede ser un valor negativo.");
        }

        if (filter.MontoMin.HasValue && filter.MontoMax.HasValue && 
            filter.MontoMin.Value >= 0 && filter.MontoMax.Value >= 0 &&
            filter.MontoMin.Value > filter.MontoMax.Value)
        {
            ModelState.AddModelError(nameof(filter.MontoMax), "El monto máximo no puede ser menor al monto mínimo.");
        }

        // 2. No aceptar rangos de fechas inválidos (fecha inicio mayor a fecha fin)
        if (filter.FechaInicio.HasValue && filter.FechaFin.HasValue && filter.FechaInicio.Value > filter.FechaFin.Value)
        {
            ModelState.AddModelError(nameof(filter.FechaInicio), "La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        // Construir la consulta base (solo solicitudes del cliente autenticado)
        var query = _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.ClienteId == cliente.Id);

        // Si las validaciones de servidor son válidas, aplicar los filtros
        if (ModelState.IsValid)
        {
            if (filter.Estado.HasValue)
            {
                query = query.Where(s => s.Estado == filter.Estado.Value);
            }

            if (filter.MontoMin.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado >= filter.MontoMin.Value);
            }

            if (filter.MontoMax.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado <= filter.MontoMax.Value);
            }

            if (filter.FechaInicio.HasValue)
            {
                var inicioUtc = DateTime.SpecifyKind(filter.FechaInicio.Value.Date, DateTimeKind.Utc);
                query = query.Where(s => s.FechaSolicitud >= inicioUtc);
            }

            if (filter.FechaFin.HasValue)
            {
                var finUtc = DateTime.SpecifyKind(filter.FechaFin.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(s => s.FechaSolicitud <= finUtc);
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Se detectaron errores en los filtros de búsqueda.";
        }

        filter.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        ViewData["KpiTotalCredito"] = $"${filter.MontoTotalAprobado:N0}";
        ViewData["KpiTotalBadge"] = $"${(filter.MontoTotalSolicitado / 1000):N1}k Solicitado";
        ViewData["KpiPendientes"] = filter.PendientesCount.ToString();
        ViewData["KpiPendientesBadge"] = "En revisión";
        ViewData["KpiLabel1"] = "Total Aprobado";
        ViewData["KpiLabel2"] = "Mis Solicitudes Pendientes";

        return View(filter);
    }

    // GET: Solicitudes/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (solicitud == null)
        {
            return NotFound();
        }

        // Seguridad: el usuario debe ser el dueño de la solicitud o tener rol Analista
        var user = await _userManager.GetUserAsync(User);
        var isAnalista = User.IsInRole("Analista");

        if (!isAnalista && (user == null || solicitud.Cliente?.UsuarioId != user.Id))
        {
            return Forbid();
        }

        // Guardar última solicitud visitada en sesión (Requerimiento para Pregunta 4 con Redis)
        HttpContext.Session.SetString("UltimaSolicitudId", solicitud.Id.ToString());
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("N0"));

        return View(solicitud);
    }
}
