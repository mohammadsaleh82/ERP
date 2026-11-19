using ERP.Modules.Tenancy.Application.Services;
using ERP.Modules.Tenancy.Contracts.Interfaces;
using ERP.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Tenancy;

public static class DependencyInjection
{
    public static IServiceCollection AddTenancyModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<TenancyDbContext>((sp, options) =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Tenancy");
                });
            }
            options.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<ITenancyMembershipValidator, TenancyMembershipValidator>();
        services.AddScoped<ITenancyModuleApi, TenancyModuleApi>();

        return services;
    }
}
