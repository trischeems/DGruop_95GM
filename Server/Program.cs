using GM95.Server.Configuration;
using GM95.Server.Infrastructure.Data;
using GM95.Server.Infrastructure.Migrations;
using GM95.Server.Infrastructure.Tenancy;
using GM95.Server.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1) NAP CAU HINH tu Server/config.json (nguon su that). Fail-fast neu thieu.
// ---------------------------------------------------------------------------
var config = ConfigLoader.Load(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(config.Server);
builder.Services.AddSingleton(config.Gm95Postgres);
builder.Services.AddSingleton(config.R2Backup);
builder.Services.AddSingleton(config.Tenancy);

// ---------------------------------------------------------------------------
// 2) KESTREL: bind port tu config (ASPNETCORE_URLS trong run_server.bat se override neu co).
// ---------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(config.Server.PortNumber));

// ---------------------------------------------------------------------------
// 3) DATA + TENANT (Dapper). 1 NpgsqlDataSource (pool) dung chung cho DB cua app.
// ---------------------------------------------------------------------------
DapperConfig.Configure();
builder.Services.AddSingleton<IDbConnectionFactory>(
    _ => new NpgsqlConnectionFactory(config.Gm95Postgres.ConnectionString));
builder.Services.AddScoped<ITenantConnection, TenantConnection>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ---------------------------------------------------------------------------
// 4) MODULES (cac app). Them app moi = them 1 dong trong ModuleRegistry.
// ---------------------------------------------------------------------------
var modules = ModuleRegistry.BuildModules();
ModuleRegistry.RegisterAll(modules, builder.Services, config);
foreach (var m in modules)
    builder.Services.AddSingleton<IAppModule>(m); // de MigrationRunner biet cac thu muc migration

builder.Services.AddSingleton<IMigrationRunner, MigrationRunner>();

// ---------------------------------------------------------------------------
// 5) MVC Controllers (kieu Controller theo yeu cau). JSON snake-friendly de doc.
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

var app = builder.Build();

// ---------------------------------------------------------------------------
// 6) MIGRATION luc khoi dong (dev: auto_create_schema=true -> tao schema + chay V*.sql).
// ---------------------------------------------------------------------------
if (config.Tenancy.AutoCreateSchema)
{
    try
    {
        var runner = app.Services.GetRequiredService<IMigrationRunner>();
        await runner.MigrateAllAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "Migration luc khoi dong that bai. Kiem tra PostgreSQL da chay chua (Server/start_pg.bat).");
        // Khong chan server khoi dong: health/db van cho kiem tra ket noi.
    }
}

// ---------------------------------------------------------------------------
// 7) PIPELINE: exception -> tenant -> routing -> controllers.
// ---------------------------------------------------------------------------
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();

app.Logger.LogInformation("GM95 Server '{Title}' | port {Port} | DB {Db}",
    config.Server.Title, config.Server.PortNumber, config.Gm95Postgres.SafeConnectionString);

app.Run();
