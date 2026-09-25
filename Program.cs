using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using creditos.Data;
using creditos.Hubs;
using creditos.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configuración dinámica del puerto asignado por Render ($PORT)
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

// Configuración de Base de Datos SQLite (soporte para disco persistente en Render /var/data/app.db)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? "Data Source=app.db";

if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var rawDataSource = connectionString.Split("Data Source=")[1].Split(';')[0].Trim();
    var dbDirectory = Path.GetDirectoryName(rawDataSource);
    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity con soporte para roles
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Configuración de Caché y Sesión con Redis (Requerimiento Pregunta 4 y Render)
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] 
    ?? builder.Configuration["Redis__ConnectionString"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? Environment.GetEnvironmentVariable("Redis__ConnectionString")
    ?? Environment.GetEnvironmentVariable("REDIS_URL");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        if (redisConnectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) || 
            redisConnectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(redisConnectionString);
            var userInfo = uri.UserInfo.Split(':');
            var password = userInfo.Length > 1 ? userInfo[1] : userInfo[0];
            var config = new ConfigurationOptions
            {
                EndPoints = { { uri.Host, uri.Port } },
                Password = password,
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
                AbortOnConnectFail = false
            };
            if (userInfo.Length > 1 && !string.IsNullOrEmpty(userInfo[0]))
            {
                config.User = userInfo[0];
            }
            options.ConfigurationOptions = config;
        }
        else
        {
            options.Configuration = redisConnectionString;
        }
        options.InstanceName = "Creditos_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Sesión respaldada por la caché distribuida (Redis en producción o Render)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Registro de servicios de dominio
builder.Services.AddScoped<ISolicitudCacheService, SolicitudCacheService>();
builder.Services.AddHttpClient<IPieSocketService, PieSocketService>();

// Cloud MQ con RabbitMQ gestionado en CloudAMQP (Requerimiento Pregunta 7)
builder.Services.AddSingleton<IRabbitMqPublisherService, RabbitMqPublisherService>();
builder.Services.AddHostedService<RabbitMqConsumerService>();

// WebSocket Hub con SignalR / PieSocket (Requerimiento Pregunta 6)
builder.Services.AddSignalR();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Inicializar la base de datos y aplicar Seed data
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

// Endpoint de WebSocket en /hubs/solicitudes protegido con Identity
app.MapHub<SolicitudesHub>("/hubs/solicitudes");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
