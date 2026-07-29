using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Infrastructure.Options;
using Bitai.LDAPGateway.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Bitai.LDAPGateway.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<LdapServerProfilesOptions>()
            .BindConfiguration(LdapServerProfilesOptions.SectionName)
            .Validate(options => options.Count > 0, "At least one LDAP server profile must be configured.")
            .ValidateOnStart();

        services.AddScoped<IBitaiLdapHelperAdapter, BitaiLdapHelperNovellAdapter>();

        services.AddScoped<ILdapGatewayClient, LdapGatewayClient>();

        // IServerProfileReadService is registered as singleton because of it's
        // a general-use service and LDAPServerProfileParameterPolicy is
        // instantiated by the routing system at application startup, before
        // any request exists. At startup, ASP.NET Core uses the root service
        // provider, which cannot resolve scoped services.
        services.AddSingleton<IServerProfileReadService, LdapServerProfileReadService>();

        services.AddScoped<IDomainEventPublisher, MediatRDomainEventPublisher>();

        return services;
    }
}
