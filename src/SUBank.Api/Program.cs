using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using SUBank.Api.HealthChecks;
using SUBank.Api.Infrastructure;
using SUBank.Api.Realtime;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Auth;
using SUBank.Infrastructure;
using SUBank.Infrastructure.Authentication;
using SUBank.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var applicationLogging = builder.Configuration
    .GetSection(ApplicationLoggingOptions.SectionName)
    .Get<ApplicationLoggingOptions>() ?? new ApplicationLoggingOptions();
var logDirectory = applicationLogging.ValidateAndResolveDirectory(builder.Environment.ContentRootPath);
if (applicationLogging.FileEnabled) Directory.CreateDirectory(logDirectory);

builder.Services.AddSerilog((_, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.With<SensitiveLogPropertyEnricher>()
        .Enrich.WithProperty("Application", "SUBank.Api")
        .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
        .WriteTo.Console(new RenderedCompactJsonFormatter());

    if (applicationLogging.FileEnabled)
    {
        loggerConfiguration.WriteTo.File(
            new RenderedCompactJsonFormatter(),
            Path.Combine(logDirectory, "subank-api-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: applicationLogging.FileSizeLimitBytes,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: applicationLogging.RetainedFileCountLimit,
            retainedFileTimeLimit: TimeSpan.FromDays(applicationLogging.RetainedDays));
    }
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>(
        "sql-server",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<RedisHealthCheck>(
        "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(3));
builder.Services.AddScoped<SignalRRealtimeNotifier>();
builder.Services.AddScoped<IRealtimeNotifier>(provider => provider.GetRequiredService<SignalRRealtimeNotifier>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddInfrastructure(builder.Configuration);
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration.");
if (string.IsNullOrWhiteSpace(jwt.Issuer))
    throw new InvalidOperationException("Jwt:Issuer is required.");
if (string.IsNullOrWhiteSpace(jwt.Audience))
    throw new InvalidOperationException("Jwt:Audience is required.");
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    throw new InvalidOperationException("Jwt:SigningKey is required.");
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes.");
if (jwt.AccessTokenMinutes is < 1 or > 60 || jwt.CustomerSessionMinutes is < 1 or > 60 ||
    jwt.RefreshTokenDays is < 1 or > 30 ||
    jwt.RefreshConcurrencyGraceSeconds is < 1 or > 300)
    throw new InvalidOperationException(
        "Jwt access token and Customer session lifetimes must be 1-60 minutes, " +
        "refresh token lifetime 1-30 days, and refresh concurrency grace 1-300 seconds.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/banking"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AccountResolution", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("Login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("TransactionPassword", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("CashDeposit", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("QrDecode", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentClient", policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:ClientOrigin"] ?? "http://localhost:5035")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(
                "WWW-Authenticate",
                CorrelationIdMiddleware.HeaderName,
                AuthProtocol.SessionIdHeader,
                AuthProtocol.RefreshCookieClearedHeader,
                AuthProtocol.SessionRevokedHeader)
            .AllowCredentials();
    });
});

var databaseInitialization = builder.Configuration
    .GetSection(DatabaseInitializationOptions.SectionName)
    .Get<DatabaseInitializationOptions>() ?? new DatabaseInitializationOptions();
if (databaseInitialization.ApplyMigrationsOnStartup && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Startup database migration is allowed only in the Development environment.");
if (databaseInitialization.SeedDemoData && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Demo seed is allowed only in the Development environment.");
if (databaseInitialization.SeedDemoData &&
    (string.IsNullOrWhiteSpace(databaseInitialization.AllowedSeedDataSource) ||
     string.IsNullOrWhiteSpace(databaseInitialization.AllowedSeedDatabase)))
    throw new InvalidOperationException(
        "DatabaseInitialization seed allow-list must contain both data source and database.");

var allowedHosts = builder.Configuration["AllowedHosts"];
var configuredHosts = allowedHosts?.Split(
    ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(allowedHosts) ||
     configuredHosts.Length == 0 ||
     configuredHosts.Contains("*", StringComparer.Ordinal)))
    throw new InvalidOperationException("Production AllowedHosts must contain explicit host names, not '*'.");

var deploymentSecurity = builder.Configuration
    .GetSection(DeploymentSecurityOptions.SectionName)
    .Get<DeploymentSecurityOptions>() ?? new DeploymentSecurityOptions();
if (deploymentSecurity.UseForwardedHeaders)
{
    if (deploymentSecurity.KnownProxies.Length == 0)
        throw new InvalidOperationException(
            "DeploymentSecurity:KnownProxies must be configured when forwarded headers are enabled.");

    var knownProxies = deploymentSecurity.KnownProxies.Select(value =>
    {
        if (!IPAddress.TryParse(value, out var address))
            throw new InvalidOperationException($"DeploymentSecurity known proxy '{value}' is not a valid IP address.");
        return address;
    }).ToArray();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        foreach (var knownProxy in knownProxies) options.KnownProxies.Add(knownProxy);
    });
}

var app = builder.Build();

if (databaseInitialization.ApplyMigrationsOnStartup || databaseInitialization.SeedDemoData)
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(databaseInitialization);
}

if (deploymentSecurity.UseForwardedHeaders) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRouting();
app.UseMiddleware<ApplicationRequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevelopmentClient");
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<RefreshCookieProtectionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ActiveSessionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<BankingHub>("/hubs/banking", options =>
    options.CloseOnAuthenticationExpiration = true);
var readinessHealthCheckOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = WriteHealthStatusAsync
};
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthStatusAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", readinessHealthCheckOptions).AllowAnonymous();
app.MapHealthChecks("/health", readinessHealthCheckOptions).AllowAnonymous();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.Map("/hubs/{**path}", () => Results.NotFound()).AllowAnonymous();
app.Map("/health/{**path}", () => Results.NotFound()).AllowAnonymous();
app.Map("/swagger/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

static Task WriteHealthStatusAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    return JsonSerializer.SerializeAsync(
        context.Response.Body,
        new { status = report.Status.ToString() },
        cancellationToken: context.RequestAborted);
}

public partial class Program;
