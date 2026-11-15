using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Domain.Services;
using ERP.Modules.Accounting.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Accounting;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AccountingDbContext>((sp, options) =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable(AccountingDbContext.MigrationsHistoryTable);
                });
            }
            options.UseSnakeCaseNamingConvention();
        });

        services.AddSingleton<IFinancialCalendarService, FinancialCalendarService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IFiscalYearService, FiscalYearService>();
        services.AddScoped<IAccountingSettingsService, AccountingSettingsService>();
        services.AddScoped<IVoucherNumberingService, VoucherNumberingService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
