using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Models;

namespace creditos.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public NotificacionesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Notificaciones
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var isAnalista = User.IsInRole("Analista");

        IQueryable<Notificacion> query = _context.Notificaciones
            .Include(n => n.Solicitud)
            .ThenInclude(s => s!.Cliente)
            .Include(n => n.Usuario);

        if (!isAnalista)
        {
            query = query.Where(n => n.UsuarioId == user.Id);
        }

        var notificaciones = await query
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();

        ViewBag.IsAnalista = isAnalista;
        return View(notificaciones);
    }
}
