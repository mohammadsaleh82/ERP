using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Modules.Accounting.Infrastructure.Persistence;

/// <summary>
/// کارخانه طراحی DbContext برای تولید Migrationهای ماژول حسابداری با dotnet-ef.
/// رشته اتصال در زمان طراحی تنها برای تشخیص Provider استفاده می‌شود.
/// </summary>
public class AccountingDbContextFactory : IDesignTimeDbContextFactory<AccountingDbContext>
{
    public AccountingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccountingDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=erp_design_time;Username=design_time;Password=design_time", npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable(AccountingDbContext.MigrationsHistoryTable);
        });
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AccountingDbContext(optionsBuilder.Options);
    }
}
