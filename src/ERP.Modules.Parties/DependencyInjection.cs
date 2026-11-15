using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Parties;

public static class DependencyInjection
{
    public static IServiceCollection AddPartiesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registration for Parties module
        return services;
    }
}
