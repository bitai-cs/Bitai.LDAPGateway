using Bitai.LDAPGateway.Api.Options;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // It is not necessary in this scenario
        // services
        //     .AddOptions<WebApiCorsConfiguration>()
        //     .BindConfiguration(WebApiCorsConfiguration.SectionName)
        //     .Validate(configuration => configuration.AllowAnyOrigin || configuration.AllowedOrigins.Count > 0,
        //         "At least one allowed origin must be configured when AllowAnyOrigin is false.")
        //     .ValidateOnStart();

        var webApiCorsConfiguration =
            configuration.GetSection(WebApiCorsConfiguration.SectionName).Get<WebApiCorsConfiguration>()
            ?? new WebApiCorsConfiguration();

        services.AddCors(options =>
        {
            options.AddPolicy(webApiCorsConfiguration.CorsPolicyName, policyBuilder =>
            {
                if (webApiCorsConfiguration.AllowAnyOrigin)
                {
                    policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                policyBuilder
                    .WithOrigins(webApiCorsConfiguration.AllowedOrigins.ToArray())
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection ConfigureRouteConstraints(this IServiceCollection services)
    {
        services.Configure<RouteOptions>(config =>
        {
            config.ConstraintMap.Add("ldapSvrPf", typeof(Controllers.ParameterPolicies.LdapServerProfileParameterPolicy));

            config.ConstraintMap.Add("ldapCatType", typeof(Controllers.ParameterPolicies.LdapCatalogTypeParameterPolicy));

            config.ConstraintMap.Add("entryId", typeof(Controllers.ParameterPolicies.DirectoryUserEntryParameterPolicy));
        });

        return services;
    }
}
