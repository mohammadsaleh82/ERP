using ERP.BuildingBlocks.Abstractions;
using ERP.BuildingBlocks.Security;
using ERP.BuildingBlocks.Tenancy;
using ERP.Modules.Accounting;
using ERP.Modules.Audit;
using ERP.Modules.Identity;
using ERP.Modules.Inventory;
using ERP.Modules.Parties;
using ERP.Modules.Payroll;
using ERP.Modules.Purchasing;
using ERP.Modules.Reporting;
using ERP.Modules.Sales;
using ERP.Modules.Subscriptions;
using ERP.Modules.Tenancy;
using ERP.Modules.Treasury;
using Microsoft.AspNetCore.Authorization;

namespace ERP.Api.Extensions;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();

        // Tenancy & Security Contexts
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        // Permission-Based Authorization
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // CORS
        services.AddCors(options =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:3000", "http://127.0.0.1:3000"];

            options.AddPolicy("DefaultCorsPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // Health Checks
        services.AddHealthChecks();

        // Register All 12 Modules
        services.AddIdentityModule(configuration);
        services.AddTenancyModule(configuration);
        services.AddAccountingModule(configuration);
        services.AddTreasuryModule(configuration);
        services.AddInventoryModule(configuration);
        services.AddPurchasingModule(configuration);
        services.AddSalesModule(configuration);
        services.AddPartiesModule(configuration);
        services.AddPayrollModule(configuration);
        services.AddReportingModule(configuration);
        services.AddSubscriptionsModule(configuration);
        services.AddAuditModule(configuration);

        return services;
    }
}
