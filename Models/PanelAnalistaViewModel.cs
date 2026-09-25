namespace creditos.Models;

public class PanelAnalistaViewModel
{
    public List<SolicitudCredito> SolicitudesPendientes { get; set; } = new();
    public decimal MontoTotalPendiente => SolicitudesPendientes.Sum(s => s.MontoSolicitado);
    public int TotalPendientes => SolicitudesPendientes.Count;
    public int SolicitudesViables => SolicitudesPendientes.Count(s => s.Cliente != null && s.MontoSolicitado <= (s.Cliente.IngresosMensuales * 5));
    public int SolicitudesExcedidas => SolicitudesPendientes.Count(s => s.Cliente != null && s.MontoSolicitado > (s.Cliente.IngresosMensuales * 5));
}
