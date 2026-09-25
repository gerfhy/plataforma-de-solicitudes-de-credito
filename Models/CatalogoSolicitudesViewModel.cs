using System.ComponentModel.DataAnnotations;

namespace creditos.Models;

public class CatalogoSolicitudesViewModel
{
    [Display(Name = "Estado")]
    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto Mínimo")]
    [Range(0, double.MaxValue, ErrorMessage = "El monto mínimo no puede ser negativo.")]
    public decimal? MontoMin { get; set; }

    [Display(Name = "Monto Máximo")]
    [Range(0, double.MaxValue, ErrorMessage = "El monto máximo no puede ser negativo.")]
    public decimal? MontoMax { get; set; }

    [Display(Name = "Fecha Desde")]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha Hasta")]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }

    public List<SolicitudCredito> Solicitudes { get; set; } = new();

    // Métricas del usuario autenticado
    public decimal MontoTotalSolicitado => Solicitudes.Sum(s => s.MontoSolicitado);
    public decimal MontoTotalAprobado => Solicitudes.Where(s => s.Estado == EstadoSolicitud.Aprobado).Sum(s => s.MontoSolicitado);
    public int PendientesCount => Solicitudes.Count(s => s.Estado == EstadoSolicitud.Pendiente);
    public int AprobadosCount => Solicitudes.Count(s => s.Estado == EstadoSolicitud.Aprobado);
    public int RechazadosCount => Solicitudes.Count(s => s.Estado == EstadoSolicitud.Rechazado);
}
