using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using SUBank.Application.Abstractions;
using SUBank.Infrastructure.Authentication;
using SUBank.Infrastructure.Banking;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;
using SUBank.Infrastructure.Profiles;
using SUBank.Infrastructure.Qr;
using SUBank.Infrastructure.Sessions;
using SUBank.Infrastructure.Statements;

namespace SUBank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        services.TryAddScoped<ICorrelationContext, NullCorrelationContext>();
        services.AddScoped<AuditCorrelationInterceptor>();
        services.AddDbContext<SUBankDbContext>((serviceProvider, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditCorrelationInterceptor>()));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 3;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromDays(36500);
            options.User.RequireUniqueEmail = false;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<SUBankDbContext>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var sessionOptions = configuration.GetSection(ActiveSessionOptions.SectionName).Get<ActiveSessionOptions>()
            ?? throw new InvalidOperationException("Missing ActiveSession configuration.");
        if (string.IsNullOrWhiteSpace(sessionOptions.RedisConnection))
            throw new InvalidOperationException("ActiveSession:RedisConnection is required.");
        if (string.IsNullOrWhiteSpace(sessionOptions.KeyPrefix))
            throw new InvalidOperationException("ActiveSession:KeyPrefix is required.");
        services.Configure<ActiveSessionOptions>(configuration.GetSection(ActiveSessionOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(sessionOptions.RedisConnection);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectTimeout = 5_000;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton<IActiveSessionStore, RedisActiveSessionStore>();
        services.AddScoped<IActiveSessionValidator, ActiveSessionValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBankingService, BankingService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IStatementService, StatementService>();
        services.AddSingleton<IStatementPdfGenerator, QuestStatementPdfGenerator>();
        services.AddScoped<IQrService, QrService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
