using Bitai.LDAPGateway.Api.Options;

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
}
