using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Api.Middleware;
using Bitai.LDAPGateway.Api.Options;
using Bitai.LDAPGateway.Application;
using Bitai.LDAPGateway.Infrastructure;
using JasperFx;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.ConfigureRouteConstraints();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddWebApiConfiguration(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwaggerUiIfConfigured();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var apiScopesConfiguration = app.Services.GetRequiredService<IOptions<ApiScopesConfiguration>>().Value;
if (apiScopesConfiguration.BypassApiScopesAuthorization)
{
    app.Logger.LogWarning("API OAuth/OIDC authorization bypass is enabled. All API endpoints allow unauthenticated callers.");
}
else
{
    app.Logger.LogInformation("API OAuth/OIDC authorization is enabled.");
}

return await app.RunJasperFxCommands(args);

public partial class Program;
