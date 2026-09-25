namespace creditos.Models;

public class DashboardViewModel
{
    public decimal MontoTotalSolicitado { get; set; }
    public decimal MontoTotalAprobado { get; set; }
    public int TotalSolicitudes { get; set; }
    public int PendientesCount { get; set; }
    public int AprobadosCount { get; set; }
    public int RechazadosCount { get; set; }
    public int TotalClientes { get; set; }
    public List<SolicitudCredito> SolicitudesRecientes { get; set; } = new();
}
