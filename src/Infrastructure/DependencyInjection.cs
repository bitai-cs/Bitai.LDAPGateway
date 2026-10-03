using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Infrastructure.Options;
using Bitai.LDAPGateway.Infrastructure.Services;
using JasperFx.CodeGeneration.Frames;
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

        services
            .AddOptions<ServiceConnectionAdapterOptions>()
            .BindConfiguration(ServiceConnectionAdapterOptions.SectionName)
            .Validate(options => options.AdapterType == LdapAdapterType.NovellLdapAdapter
                || options.AdapterType == LdapAdapterType.MockAdapter,
                "AdapterType must be either 'NovellLdapAdapter' or 'MockAdapter'.")
            .ValidateOnStart();

        var adapterOptions = configuration
            .GetSection(ServiceConnectionAdapterOptions.SectionName)
            .Get<ServiceConnectionAdapterOptions>() ?? new();

        switch (adapterOptions.AdapterType)
        {
            case LdapAdapterType.NovellLdapAdapter:
                services.AddScoped<LDAPHelper.LdapAdapters.ILdapConnectionFactoryAdapter, LDAPHelper.LdapAdapters.Novell.NovellLdapConnectionFactoryAdapter>();
                break;
            case LdapAdapterType.MockAdapter:
                services.AddScoped<LDAPHelper.LdapAdapters.ILdapConnectionFactoryAdapter, LDAPHelper.LdapAdapters.LdapHelperMock.MockLdapPersistentConnectionFactoryAdapter>();
                break;
            default:
                throw new InvalidOperationException($"Unsupported adapter type: {adapterOptions.AdapterType}");
        }

        services.AddScoped<IDirectoryServiceProvider, BitaiLdapHelperProvider>();

        services.AddScoped<IDirectoryServiceConnector, DirectoryServiceConnector>();
        
        services.AddScoped<IServerProfileReadService, LdapServerProfileReadService>();

        services.AddScoped<IDomainEventPublisher, WolverineDomainEventPublisher>();

        return services;
    }
}
