using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Models;

namespace creditos.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var solicitudes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        var totalClientes = await _context.Clientes.CountAsync();

        var viewModel = new DashboardViewModel
        {
            TotalSolicitudes = solicitudes.Count,
            PendientesCount = solicitudes.Count(s => s.Estado == EstadoSolicitud.Pendiente),
            AprobadosCount = solicitudes.Count(s => s.Estado == EstadoSolicitud.Aprobado),
            RechazadosCount = solicitudes.Count(s => s.Estado == EstadoSolicitud.Rechazado),
            MontoTotalSolicitado = solicitudes.Sum(s => s.MontoSolicitado),
            MontoTotalAprobado = solicitudes.Where(s => s.Estado == EstadoSolicitud.Aprobado).Sum(s => s.MontoSolicitado),
            TotalClientes = totalClientes,
            SolicitudesRecientes = solicitudes.Take(5).ToList()
        };

        ViewData["KpiTotalCredito"] = $"${viewModel.MontoTotalAprobado:N0}";
        ViewData["KpiTotalBadge"] = $"${(viewModel.MontoTotalSolicitado / 1000):N1}k Solicitado";
        ViewData["KpiPendientes"] = viewModel.PendientesCount.ToString();
        ViewData["KpiPendientesBadge"] = "En espera";
        ViewData["KpiLabel1"] = "Total Aprobado";
        ViewData["KpiLabel2"] = "Solicitudes Pendientes";

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
