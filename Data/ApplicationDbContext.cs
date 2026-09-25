using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using creditos.Models;

namespace creditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Cliente configuration
        builder.Entity<Cliente>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.IngresosMensuales)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SolicitudCredito configuration
        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.MontoSolicitado)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restricción: Un cliente solo puede tener una solicitud en estado Pendiente
            entity.HasIndex(s => s.ClienteId)
                .HasFilter("[Estado] = 0")
                .IsUnique();
        });
    }
}
