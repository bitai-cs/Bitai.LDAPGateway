using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Infrastructure.Options;
using Bitai.LDAPGateway.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // For product code, register the real implementation of ILdapConnectionFactory
        //services.AddScoped<LDAPHelper.LdapAdapters.ILdapConnectionFactoryAdapter, LDAPHelper.LdapAdapters.Novell.NovellLdapConnectionFactoryAdapter>();

        // For testing purposes, we can use a mock connection factory adapter
        services.AddSingleton<LDAPHelper.LdapAdapters.ILdapConnectionFactoryAdapter, LDAPHelper.LdapAdapters.LdapHelperMock.MockLdapPersistentConnectionFactoryAdapter>();

        services.AddScoped<IDirectoryServiceProvider, BitaiLdapHelperProvider>();

        services.AddScoped<IDirectoryServiceConnector, DirectoryServiceConnector>();
        
        services.AddScoped<IServerProfileReadService, LdapServerProfileReadService>();

        services.AddScoped<IDomainEventPublisher, WolverineDomainEventPublisher>();

        return services;
    }
}
