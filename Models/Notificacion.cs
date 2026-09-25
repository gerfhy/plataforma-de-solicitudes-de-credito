using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace creditos.Models;

public class Notificacion
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(64)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    public int SolicitudId { get; set; }

    [ForeignKey(nameof(SolicitudId))]
    public SolicitudCredito? Solicitud { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [ForeignKey(nameof(UsuarioId))]
    public IdentityUser? Usuario { get; set; }

    [Required]
    [StringLength(500)]
    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; } = DateTime.UtcNow;
}
