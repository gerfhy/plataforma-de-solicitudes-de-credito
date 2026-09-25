using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Models;
using creditos.Services;

namespace creditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ISolicitudCacheService _cacheService;

    public SolicitudesController(
        ApplicationDbContext context, 
        UserManager<IdentityUser> userManager,
        ISolicitudCacheService cacheService)
    {
        _context = context;
        _userManager = userManager;
        _cacheService = cacheService;
    }

    // GET: Solicitudes (Mis Solicitudes con filtros y Caché Redis de 60s)
    public async Task<IActionResult> Index(CatalogoSolicitudesViewModel filter)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await ObtenerOCrearClienteAsync(user.Id);

        // Validaciones Server-Side requeridas por Pregunta 2:
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

        if (filter.FechaInicio.HasValue && filter.FechaFin.HasValue && filter.FechaInicio.Value > filter.FechaFin.Value)
        {
            ModelState.AddModelError(nameof(filter.FechaInicio), "La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        // --- MANEJO DE CACHÉ REDIS (60 SEGUNDOS) - PREGUNTA 4 ---
        // Intentar obtener listado de solicitudes del usuario desde la caché
        var solicitudesUsuario = await _cacheService.ObtenerSolicitudesCacheAsync(user.Id);
        bool cacheHit = false;

        if (solicitudesUsuario != null)
        {
            cacheHit = true;
        }
        else
        {
            // Consultar a SQLite y guardar en caché por 60s
            solicitudesUsuario = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .Where(s => s.ClienteId == cliente.Id)
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();

            await _cacheService.GuardarSolicitudesCacheAsync(user.Id, solicitudesUsuario);
        }

        ViewData["CachéHit"] = cacheHit;

        // Aplicar filtros server-side sobre la colección
        IEnumerable<SolicitudCredito> query = solicitudesUsuario;

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

        filter.Solicitudes = query.ToList();

        ViewData["KpiTotalCredito"] = $"${filter.MontoTotalAprobado:N0}";
        ViewData["KpiTotalBadge"] = $"${(filter.MontoTotalSolicitado / 1000):N1}k Solicitado";
        ViewData["KpiPendientes"] = filter.PendientesCount.ToString();
        ViewData["KpiPendientesBadge"] = "En revisión";
        ViewData["KpiLabel1"] = "Total Aprobado";
        ViewData["KpiLabel2"] = "Mis Solicitudes Pendientes";

        return View(filter);
    }

    // GET: Solicitudes/Create (Formulario de registro)
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await ObtenerOCrearClienteAsync(user.Id);

        var solicitudPendiente = await _context.SolicitudesCredito
            .FirstOrDefaultAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        var viewModel = new CrearSolicitudViewModel
        {
            IngresosMensuales = cliente.IngresosMensuales,
            ClienteActivo = cliente.Activo,
            TieneSolicitudPendiente = solicitudPendiente != null,
            SolicitudPendienteExistenteId = solicitudPendiente?.Id
        };

        if (viewModel.TieneSolicitudPendiente)
        {
            ViewData["AlertaPendiente"] = $"Actualmente tienes la solicitud #{solicitudPendiente!.Id} en estado Pendiente. No es posible crear una nueva hasta que sea evaluada.";
        }

        if (!viewModel.ClienteActivo)
        {
            ViewData["AlertaInactivo"] = "Tu estado de cliente se encuentra inactivo. Por favor contacta al analista de créditos.";
        }

        return View(viewModel);
    }

    // POST: Solicitudes/Create (Registro, validaciones y invalidación de caché)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrearSolicitudViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Debes estar autenticado para registrar una solicitud.");
            return View(model);
        }

        var cliente = await ObtenerOCrearClienteAsync(user.Id);

        model.IngresosMensuales = cliente.IngresosMensuales;
        model.ClienteActivo = cliente.Activo;

        // Validaciones Server-Side
        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "Tu cuenta de cliente está inactiva. No tienes autorización para solicitar créditos.");
        }

        var solicitudPendiente = await _context.SolicitudesCredito
            .FirstOrDefaultAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (solicitudPendiente != null)
        {
            model.TieneSolicitudPendiente = true;
            model.SolicitudPendienteExistenteId = solicitudPendiente.Id;
            ModelState.AddModelError(string.Empty, 
                $"Ya tienes una solicitud pendiente de evaluación (#{solicitudPendiente.Id}). La política de negocio solo permite una solicitud activa por cliente.");
        }

        if (model.MontoSolicitado <= 0)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), "El monto solicitado debe ser mayor a $0.");
        }

        var limiteMaximo = cliente.IngresosMensuales * 10;
        if (model.MontoSolicitado > limiteMaximo)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), 
                $"El monto solicitado (${model.MontoSolicitado:N2}) supera el límite máximo permitido de 10 veces tus ingresos mensuales (${limiteMaximo:N2}).");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "No se pudo registrar la solicitud. Por favor revisa las validaciones indicadas.";
            return View(model);
        }

        var nuevaSolicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente,
            MotivoRechazo = null
        };

        _context.SolicitudesCredito.Add(nuevaSolicitud);
        await _context.SaveChangesAsync();

        // 1. Guardar última solicitud visitada/creada en Sesión respaldada por Redis
        HttpContext.Session.SetString("UltimaSolicitudId", nuevaSolicitud.Id.ToString());
        HttpContext.Session.SetString("UltimaSolicitudMonto", nuevaSolicitud.MontoSolicitado.ToString("N0"));

        // 2. INVALIDACIÓN DE CACHÉ REDIS (Requerido: invalidar cuando se registre una nueva solicitud)
        await _cacheService.InvalidarCacheUsuarioAsync(user.Id);

        model.RegistroExitoso = true;
        model.SolicitudCreadaId = nuevaSolicitud.Id;
        model.MontoCreado = nuevaSolicitud.MontoSolicitado;
        model.TieneSolicitudPendiente = true;
        model.SolicitudPendienteExistenteId = nuevaSolicitud.Id;

        TempData["SuccessMessage"] = $"¡Solicitud #{nuevaSolicitud.Id} registrada exitosamente por ${nuevaSolicitud.MontoSolicitado:N2} en estado Pendiente!";

        return View(model);
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

        var user = await _userManager.GetUserAsync(User);
        var isAnalista = User.IsInRole("Analista");

        if (!isAnalista && (user == null || solicitud.Cliente?.UsuarioId != user.Id))
        {
            return Forbid();
        }

        // Sesión con Redis: Guardar última solicitud visitada
        HttpContext.Session.SetString("UltimaSolicitudId", solicitud.Id.ToString());
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("N0"));

        return View(solicitud);
    }

    private async Task<Cliente> ObtenerOCrearClienteAsync(string usuarioId)
    {
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente == null)
        {
            cliente = new Cliente
            {
                UsuarioId = usuarioId,
                IngresosMensuales = 3500.00m,
                Activo = true
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
        }
        return cliente;
    }
}
