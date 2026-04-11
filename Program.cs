using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var isProd = builder.Environment.IsProduction();

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase($"gasoholic-{Guid.NewGuid():N}"));
}
else if (builder.Environment.IsDevelopment())
{
    var devConnStr = builder.Configuration.GetConnectionString("Cosmos")
        ?? throw new InvalidOperationException("Cosmos connection string not configured for Development.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseCosmos(devConnStr, databaseName: "gasoholic", cosmosOptions =>
        {
            cosmosOptions.HttpClientFactory(() =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                return new HttpClient(handler);
            });
            cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
        }));
}
else
{
    var connStr = builder.Configuration.GetConnectionString("Cosmos")
        ?? throw new InvalidOperationException("Cosmos connection string not configured.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseCosmos(connStr, databaseName: "gasoholic"));
}

builder.Services.AddDistributedMemoryCache();

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
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
fwdOptions.KnownIPNetworks.Clear();
fwdOptions.KnownProxies.Clear();
app.UseForwardedHeaders(fwdOptions);

// Log full exception details (inner exceptions) to stdout for diagnostics
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Unhandled exception on {context.Request.Method} {context.Request.Path}:");
        Console.Error.WriteLine(ex.ToString());
        throw;
    }
});

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
