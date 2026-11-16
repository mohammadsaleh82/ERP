using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Treasury;

public static class DependencyInjection
{
    public static IServiceCollection AddTreasuryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registration for Treasury module
        return services;
    }
}
