using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var dbProvider = (Environment.GetEnvironmentVariable("DATABASE_PROVIDER") ?? "sqlite").ToLower();
var isProd = builder.Environment.IsProduction();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider == "sqlserver")
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    // Suppress false-positive warning caused by EF tools/runtime version mismatch
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

if (dbProvider == "sqlserver")
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("SqlServer");
        options.SchemaName = "dbo";
        options.TableName = "SessionCache";
        options.DefaultSlidingExpiration = TimeSpan.FromDays(30);
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

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

// Serve Angular dist from wwwroot/browser/ (Angular 17+ output structure)
app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new[] { "index.html" } });
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "browser")
    ),
    RequestPath = ""
});

// SPA fallback: any route not matched by files/API routes gets index.html
// This allows Angular Router to handle client-side routing
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "browser")
    )
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
app.MapSmokeTestEndpoints();

app.Run();

public partial class Program { }
