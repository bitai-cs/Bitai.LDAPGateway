using Bitai.LDAPGateway.Api.Options;
using NSwag;

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

    public static IServiceCollection AddWebApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<WebApiConfigurationOptions>()
            .BindConfiguration(WebApiConfigurationOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.WebApiTitle), "WebApiTitle is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.WebApiVersion), "WebApiVersion is required.")
            .ValidateOnStart();

        var webApiConfig = configuration
            .GetSection(WebApiConfigurationOptions.SectionName)
            .Get<WebApiConfigurationOptions>() ?? new();

        services.AddSingleton(webApiConfig);

        services.AddOpenApiDocument(config =>
        {
            config.DocumentName = webApiConfig.WebApiName;

            config.PostProcess = document =>
            {
                document.Info.Title = webApiConfig.WebApiTitle;
                document.Info.Description = webApiConfig.WebApiDescription;
                document.Info.Version = webApiConfig.WebApiVersion;
                document.Info.Contact = new OpenApiContact
                {
                    Name = webApiConfig.WebApiContactName,
                    Email = webApiConfig.WebApiContactMail,
                    Url = webApiConfig.WebApiContactUrl
                };
                document.Info.License = new OpenApiLicense
                {
                    Name = webApiConfig.WebApiLicenseName
                };
            };
        });

        return services;
    }

    public static IServiceCollection ConfigureRouteConstraints(this IServiceCollection services)
    {
        // Register constraint classes in the DI container
        services.AddScoped<Controllers.RouteConstraints.DirectoryServerProfileRouteConstraint>();
        services.AddScoped<Controllers.RouteConstraints.DirectoryCatalogTypeRouteConstraint>();
        //services.AddScoped<Controllers.RouteConstraints.DirectoryUserEntryRouteConstraint>();
        //services.AddScoped<Controllers.RouteConstraints.DirectoryGroupRouteConstraint>();

        // Register constraints in the routing system
        services.Configure<RouteOptions>(config =>
        {
            config.ConstraintMap.Add("ldapSvrPf", typeof(Controllers.RouteConstraints.DirectoryServerProfileRouteConstraint));
            config.ConstraintMap.Add("ldapCatType", typeof(Controllers.RouteConstraints.DirectoryCatalogTypeRouteConstraint));
            //config.ConstraintMap.Add("userId", typeof(Controllers.RouteConstraints.DirectoryUserEntryRouteConstraint));
            //config.ConstraintMap.Add("groupId", typeof(Controllers.RouteConstraints.DirectoryGroupRouteConstraint));
        });

        return services;
    }
}
