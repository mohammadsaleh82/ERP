using ERP.Modules.Accounting.Application.Services;
using ERP.Modules.Accounting.Application.Validators;
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
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IVoucherPostingEngine, VoucherPostingEngine>();
        services.AddScoped<IBalanceEngine, BalanceEngine>();
        services.AddScoped<IBalanceSnapshotService, BalanceSnapshotService>();
        services.AddScoped<ILedgerQueryService, LedgerQueryService>();
        services.AddScoped<IGlConsistencyService, GlConsistencyService>();
        services.AddScoped<IDimensionValidationService, DimensionValidationApplicationService>();
        services.AddScoped<IFinancialDimensionService, FinancialDimensionService>();
        services.AddScoped<IDimensionAnalysisService, DimensionAnalysisService>();
        services.AddScoped<Contracts.Interfaces.IFinancialDimensionApi, Application.Services.FinancialDimensionApi>();
        services.AddSingleton<Contracts.Interfaces.IOpeningInventorySource, Contracts.Interfaces.EmptyOpeningInventorySource>();
        services.AddScoped<IOpeningVoucherService, OpeningVoucherService>();
        services.AddScoped<IAdjustmentVoucherService, AdjustmentVoucherService>();
        services.AddScoped<IFiscalClosingWorkflowService, FiscalClosingWorkflowService>();
        services.AddScoped<IFiscalYearReopeningService, FiscalYearReopeningService>();
        services.AddScoped<Contracts.Interfaces.IAccountingModuleApi, Application.Services.AccountingModuleApi>();
        services.AddScoped<CreateDocumentRequestValidator>();
        services.AddScoped<UpdateDocumentRequestValidator>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
