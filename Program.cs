using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var isProd = builder.Environment.IsProduction();

var connStrBase = builder.Configuration.GetConnectionString("SqlServer")
    ?? throw new InvalidOperationException("SqlServer connection string not configured.");
// Local dev: SA_PASSWORD is in .env and appended here (connection string base has no password).
// Production: full connection string (including password) is injected via Key Vault — SA_PASSWORD not needed.
var saPassword = Environment.GetEnvironmentVariable("SA_PASSWORD");
var connStr = saPassword != null ? $"{connStrBase}Password={saPassword};" : connStrBase;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connStr);
    // Suppress false-positive warning caused by EF tools/runtime version mismatch
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = connStr;
    options.SchemaName = "dbo";
    options.TableName = "SessionCache";
    options.DefaultSlidingExpiration = TimeSpan.FromDays(30);
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.MaxAge = TimeSpan.FromDays(30);  // persistent — survives browser close
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isProd
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.None;
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddSingleton<IVerificationEmailSender, VerificationEmailSender>();
builder.Services.AddHostedService<VerificationCleanupService>();

var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
    ?? builder.Configuration["CorsOrigins"]
    ?? "http://localhost:5000,https://localhost:5001")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
fwdOptions.KnownIPNetworks.Clear();
fwdOptions.KnownProxies.Clear();
app.UseForwardedHeaders(fwdOptions);
app.UseCors();
app.UseSession();

var wwwrootBrowser = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "browser");
Directory.CreateDirectory(wwwrootBrowser);

app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new[] { "index.html" } });
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootBrowser),
    RequestPath = ""
});

// SPA fallback: any route not matched by files/API routes gets index.html
// This allows Angular Router to handle client-side routing
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootBrowser)
});

app.MapGet("/health", (IVerificationEmailSender emailSender) => Results.Ok(new
{
    status = "ok",
    email = new
    {
        configured = emailSender.IsConfigured,
        senderDomain = emailSender.SenderDomain,
        senderAddress = emailSender.SenderAddress
    }
}));

app.MapAuthEndpoints();
app.MapAutoEndpoints();
app.MapFillupEndpoints();
app.MapMaintenanceEndpoints();
app.MapSmokeTestEndpoints();

app.Run();

public partial class Program { }
