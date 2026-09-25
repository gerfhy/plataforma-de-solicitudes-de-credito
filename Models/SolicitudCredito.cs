using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace creditos.Models;

public class SolicitudCredito
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    [ForeignKey(nameof(ClienteId))]
    public Cliente? Cliente { get; set; }

    [Required(ErrorMessage = "El monto solicitado es requerido.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Monto Solicitado")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Fecha de Solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    [Required]
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [Display(Name = "Motivo de Rechazo")]
    [StringLength(500, ErrorMessage = "El motivo de rechazo no puede superar los 500 caracteres.")]
    public string? MotivoRechazo { get; set; }
}
