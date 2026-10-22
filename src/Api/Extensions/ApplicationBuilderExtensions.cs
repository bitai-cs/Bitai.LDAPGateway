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
            app.UseOpenApi();
            app.UseSwaggerUi();
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
