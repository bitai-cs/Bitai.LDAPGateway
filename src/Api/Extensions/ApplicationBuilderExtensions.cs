using Bitai.LDAPGateway.Api.Options;
using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwaggerUiIfConfigured(this IApplicationBuilder app)
    {
        var webApiConfig = app.ApplicationServices.GetRequiredService<WebApiConfigurationOptions>();

        if (webApiConfig.SwaggerUI)
        {
            var apiScopesConfiguration = app.ApplicationServices.GetRequiredService<IOptions<ApiScopesConfiguration>>().Value;

            app.UseOpenApi();
            app.UseSwaggerUi(settings =>
            {
                if (apiScopesConfiguration.BypassApiScopesAuthorization)
                {
                    return;
                }

                var swaggerUiClient = app.ApplicationServices.GetRequiredService<IOptions<SwaggerUIApiClientOptions>>().Value;
                settings.OAuth2Client = new NSwag.AspNetCore.OAuth2ClientSettings
                {
                    ClientId = swaggerUiClient.SwaggerUIClientId,
                    ClientSecret = swaggerUiClient.SwaggerUIClientSecret,
                    UsePkceWithAuthorizationCodeGrant = true
                };
            });
        }

        return app;
    }

    public static IApplicationBuilder LogSecurityByPassConfiguration(this WebApplication app)
    {
        var apiScopesConfiguration = app.Services.GetRequiredService<IOptions<ApiScopesConfiguration>>().Value;
        if (apiScopesConfiguration.BypassApiScopesAuthorization)
        {
            app.Logger.LogWarning("API OAuth/OIDC authorization bypass is enabled. All API endpoints allow unauthenticated callers.");
        }
        else
        {
            app.Logger.LogInformation("API OAuth/OIDC authorization is enabled.");
        }

        return app;
    }
}
