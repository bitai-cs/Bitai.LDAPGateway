using Bitai.LDAPGateway.Application.Common.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace Bitai.LDAPGateway.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handler discovery and FluentValidation are configured by Wolverine in the host (UseWolverine).
        // The cache façade reads IFusionCache and ICacheInvalidator, both registered by Infrastructure.
        services.AddScoped<DirectoryCache>();

        return services;
    }
}
