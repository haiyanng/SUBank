using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;
using SUBank.Application.Abstractions;
using SUBank.Infrastructure.Authentication;
using SUBank.Infrastructure.Banking;
using SUBank.Infrastructure.Sessions;
using StackExchange.Redis;

namespace SUBank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Missing DefaultConnection.");
        services.AddDbContext<SUBankDbContext>(options => options.UseSqlServer(connectionString));
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
        services.Configure<ActiveSessionOptions>(configuration.GetSection(ActiveSessionOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(sessionOptions.RedisConnection);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectTimeout = 5_000;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddSingleton<IActiveSessionStore, RedisActiveSessionStore>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBankingService, BankingService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
