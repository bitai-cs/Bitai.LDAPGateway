using Bitai.LDAPGateway.Api.Options;
using Bitai.LDAPGateway.Api.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NSwag;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Name of the OAuth2 security scheme declared in the OpenAPI document.</summary>
    public const string SwaggerOAuth2SchemeName = "oauth2";

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
            .AddOptions<SwaggerUIApiClientOptions>()
            .BindConfiguration(SwaggerUIApiClientOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SwaggerUIClientId),
                "Swagger UI client id is required when API security bypass is disabled.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SwaggerUIClientSecret),
                "Swagger UI client secret is required when API security bypass is disabled.")
            .Validate(
                options => options.SwaggerUITargetApiScopes.Count > 0 &&
                           options.SwaggerUITargetApiScopes.All(
                               scope => !string.IsNullOrWhiteSpace(scope.SwaggerUITargetApiScopeName)),
                "At least one Swagger UI target API scope with a non-blank name is required when API security bypass is disabled.")
            .Validate(
                options => options.SwaggerUITargetApiScopes.All(
                    scope => string.Equals(scope.SwaggerUITargetApiScopeName, scopesConfiguration.AdminApiScopeName, StringComparison.Ordinal) ||
                             string.Equals(scope.SwaggerUITargetApiScopeName, scopesConfiguration.ReaderApiScopeName, StringComparison.Ordinal)),
                "Swagger UI target API scopes must match the configured administrator or reader API scope names.")
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
        // CORS options are read directly below; startup validation of the origins list is intentionally not registered in this scenario.

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

        services.AddOpenApiDocument((config, serviceProvider) =>
        {
            config.DocumentName = webApiConfig.WebApiName;

            var apiScopesConfiguration = serviceProvider.GetRequiredService<IOptions<ApiScopesConfiguration>>().Value;
            if (!apiScopesConfiguration.BypassApiScopesAuthorization)
            {
                var authorityConfiguration = serviceProvider.GetRequiredService<IOptions<AuthorityConfiguration>>().Value;
                var swaggerUiClient = serviceProvider.GetRequiredService<IOptions<SwaggerUIApiClientOptions>>().Value;
                var authority = authorityConfiguration.Authority.TrimEnd('/');

                config.AddSecurity(SwaggerOAuth2SchemeName, Enumerable.Empty<string>(), new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.OAuth2,
                    Description = "OAuth/OIDC authorization code flow against the configured authority.",
                    Flow = OpenApiOAuth2Flow.AccessCode,
                    AuthorizationUrl = $"{authority}/connect/authorize",
                    TokenUrl = $"{authority}/connect/token",
                    Scopes = swaggerUiClient.SwaggerUITargetApiScopes.ToDictionary(
                        scope => scope.SwaggerUITargetApiScopeName,
                        scope => scope.SwaggerUITargetApiScopeTitle)
                });
            }

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

        // Route constraints are limited to synchronous, local validation: no I/O or external calls.

        // Register constraints in the routing system
        services.Configure<RouteOptions>(config =>
        {
            config.ConstraintMap.Add("ldapSvrPf", typeof(Controllers.RouteConstraints.DirectoryServerProfileRouteConstraint));
            config.ConstraintMap.Add("ldapCatType", typeof(Controllers.RouteConstraints.DirectoryCatalogTypeRouteConstraint));
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
