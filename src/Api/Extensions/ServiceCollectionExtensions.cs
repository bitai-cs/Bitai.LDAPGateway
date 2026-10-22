using Bitai.LDAPGateway.Api.Options;
using Bitai.LDAPGateway.Api.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NSwag;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var scopesConfiguration = configuration
            .GetSection(ApiScopesConfiguration.SectionName)
            .Get<ApiScopesConfiguration>() ?? new ApiScopesConfiguration();

        services
            .AddOptions<ApiScopesConfiguration>()
            .BindConfiguration(ApiScopesConfiguration.SectionName)
            .Validate(
                options => options.BypassApiScopesAuthorization ||
                           (!string.IsNullOrWhiteSpace(options.AdminApiScopeName) &&
                            !string.IsNullOrWhiteSpace(options.ReaderApiScopeName) &&
                            !string.Equals(
                                options.AdminApiScopeName,
                                options.ReaderApiScopeName,
                                StringComparison.Ordinal)),
                "Administrator and reader API scopes must be non-blank and distinct when API security bypass is disabled.")
            .ValidateOnStart();

        if (scopesConfiguration.BypassApiScopesAuthorization)
        {
            services.AddAuthentication();
            services.AddAuthorization(options =>
            {
                var allowAll = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build();

                options.FallbackPolicy = allowAll;
                options.AddPolicy(ApiAuthorizationPolicies.Administrator, allowAll);
                options.AddPolicy(ApiAuthorizationPolicies.ReaderOrAdministrator, allowAll);
            });

            return services;
        }

        var authorityConfiguration = configuration
            .GetSection(AuthorityConfiguration.SectionName)
            .Get<AuthorityConfiguration>() ?? new AuthorityConfiguration();

        services
            .AddOptions<AuthorityConfiguration>()
            .BindConfiguration(AuthorityConfiguration.SectionName)
            .Validate(
                options => Uri.TryCreate(options.Authority, UriKind.Absolute, out _),
                "Authority must be an absolute URI when API security bypass is disabled.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiResource),
                "API resource is required when API security bypass is disabled.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authorityConfiguration.Authority;
                options.Audience = authorityConfiguration.ApiResource;
                options.RequireHttpsMetadata = authorityConfiguration.RequireHttpsMetadata;
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = CreateScopePolicy(scopesConfiguration.AdminApiScopeName);
            options.AddPolicy(
                ApiAuthorizationPolicies.Administrator,
                CreateScopePolicy(scopesConfiguration.AdminApiScopeName));
            options.AddPolicy(
                ApiAuthorizationPolicies.ReaderOrAdministrator,
                CreateScopePolicy(
                    scopesConfiguration.AdminApiScopeName,
                    scopesConfiguration.ReaderApiScopeName));
        });

        return services;
    }

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

        // Route constraints remain limited to synchronous, local validation, so it's not a good practice calling I/O or external services in the route constraint.
        //services.AddScoped<Controllers.RouteConstraints.DirectoryUserEntryRouteConstraint>();
        //services.AddScoped<Controllers.RouteConstraints.DirectoryGroupRouteConstraint>();

        // Register constraints in the routing system
        services.Configure<RouteOptions>(config =>
        {
            config.ConstraintMap.Add("ldapSvrPf", typeof(Controllers.RouteConstraints.DirectoryServerProfileRouteConstraint));
            config.ConstraintMap.Add("ldapCatType", typeof(Controllers.RouteConstraints.DirectoryCatalogTypeRouteConstraint));

            // Route constraints remain limited to synchronous, local validation, so it's not a good practice calling I/O or external services in the route constraint.
            //config.ConstraintMap.Add("userId", typeof(Controllers.RouteConstraints.DirectoryUserEntryRouteConstraint));
            //config.ConstraintMap.Add("groupId", typeof(Controllers.RouteConstraints.DirectoryGroupRouteConstraint));
        });

        return services;
    }

    private static AuthorizationPolicy CreateScopePolicy(params string[] acceptedScopes)
    {
        return new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.Claims
                .Where(claim => claim.Type == "scope")
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Any(scope => acceptedScopes.Contains(scope, StringComparer.Ordinal)))
            .Build();
    }
}
