using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;
using SUBank.Application.Abstractions;
using SUBank.Infrastructure.Authentication;
using SUBank.Infrastructure.Banking;

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
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBankingService, BankingService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
