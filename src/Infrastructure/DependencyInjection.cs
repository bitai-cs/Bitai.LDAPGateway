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

      services.AddScoped<IBitaiLdapHelperAdapter, BitaiLdapHelperAdapter>();
      services.AddScoped<ILdapGatewayClient, LdapGatewayClient>();
      services.AddScoped<IServerProfileReadService, LdapServerProfileReadService>();
      services.AddScoped<IDomainEventPublisher, MediatRDomainEventPublisher>();

      return services;
   }
}
