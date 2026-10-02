using Microsoft.Extensions.DependencyInjection;

namespace Bitai.LDAPGateway.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handler discovery and FluentValidation are configured by Wolverine in the host (UseWolverine).
        return services;
    }
}
