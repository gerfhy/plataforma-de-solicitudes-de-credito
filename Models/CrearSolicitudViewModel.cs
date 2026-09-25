using System.ComponentModel.DataAnnotations;

namespace creditos.Models;

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "Debes ingresar el monto a solicitar.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Display(Name = "Monto Solicitado ($)")]
    public decimal MontoSolicitado { get; set; }

    // Información contextual del cliente para la vista
    public decimal IngresosMensuales { get; set; }
    public decimal LimiteMaximo10x => IngresosMensuales * 10;
    public decimal CapacidadAprobacion5x => IngresosMensuales * 5;
    public bool ClienteActivo { get; set; } = true;
    public bool TieneSolicitudPendiente { get; set; }
    public int? SolicitudPendienteExistenteId { get; set; }

    // Feedback de éxito en la misma vista
    public bool RegistroExitoso { get; set; }
    public int? SolicitudCreadaId { get; set; }
    public decimal? MontoCreado { get; set; }
}
