using Bitai.LDAPGateway.Api.Options;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSwaggerUiIfConfigured(this IApplicationBuilder app)
    {
        var webApiConfig = app.Services.GetRequiredService<WebApiConfigurationOptions>();

        if (webApiConfig.SwaggerUI)
        {
            app.UseOpenApi();
            app.UseSwaggerUi();
        }

        return app;
    }
}
