using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using creditos.Models;

namespace creditos.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Aplicar migraciones pendientes automáticamente
        await context.Database.MigrateAsync();

        // 1. Crear Rol Analista
        const string analistaRole = "Analista";
        if (!await roleManager.RoleExistsAsync(analistaRole))
        {
            await roleManager.CreateAsync(new IdentityRole(analistaRole));
        }

        // 2. Crear usuario con rol Analista
        const string analistaEmail = "analista@creditos.com";
        var analistaUser = await userManager.FindByEmailAsync(analistaEmail);
        if (analistaUser == null)
        {
            analistaUser = new IdentityUser
            {
                UserName = analistaEmail,
                Email = analistaEmail,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(analistaUser, "Password123!");
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(analistaUser, analistaRole);
            }
        }

        // 3. Crear Cliente 1 y Solicitud Pendiente
        const string cliente1Email = "cliente1@creditos.com";
        var cliente1User = await userManager.FindByEmailAsync(cliente1Email);
        if (cliente1User == null)
        {
            cliente1User = new IdentityUser
            {
                UserName = cliente1Email,
                Email = cliente1Email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(cliente1User, "Password123!");
        }

        var cliente1 = await context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == cliente1User.Id);
        if (cliente1 == null)
        {
            cliente1 = new Cliente
            {
                UsuarioId = cliente1User.Id,
                IngresosMensuales = 3500.00m,
                Activo = true
            };
            context.Clientes.Add(cliente1);
            await context.SaveChangesAsync();
        }

        if (!await context.SolicitudesCredito.AnyAsync(s => s.ClienteId == cliente1.Id))
        {
            context.SolicitudesCredito.Add(new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 5000.00m,
                FechaSolicitud = DateTime.UtcNow.AddDays(-2),
                Estado = EstadoSolicitud.Pendiente,
                MotivoRechazo = null
            });
            await context.SaveChangesAsync();
        }

        // 4. Crear Cliente 2 y Solicitud Aprobada
        const string cliente2Email = "cliente2@creditos.com";
        var cliente2User = await userManager.FindByEmailAsync(cliente2Email);
        if (cliente2User == null)
        {
            cliente2User = new IdentityUser
            {
                UserName = cliente2Email,
                Email = cliente2Email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(cliente2User, "Password123!");
        }

        var cliente2 = await context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == cliente2User.Id);
        if (cliente2 == null)
        {
            cliente2 = new Cliente
            {
                UsuarioId = cliente2User.Id,
                IngresosMensuales = 4000.00m,
                Activo = true
            };
            context.Clientes.Add(cliente2);
            await context.SaveChangesAsync();
        }

        if (!await context.SolicitudesCredito.AnyAsync(s => s.ClienteId == cliente2.Id))
        {
            context.SolicitudesCredito.Add(new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 12000.00m, // 12000 <= 5 * 4000 (20000)
                FechaSolicitud = DateTime.UtcNow.AddDays(-5),
                Estado = EstadoSolicitud.Aprobado,
                MotivoRechazo = null
            });
            await context.SaveChangesAsync();
        }
    }
}
