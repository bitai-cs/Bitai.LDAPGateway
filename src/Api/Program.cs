using Bitai.LDAPGateway.Application.Common.Behaviors;
using Wolverine.FluentValidation;
using Wolverine;
using JasperFx.CodeGeneration;
using JasperFx;
using Bitai.LDAPGateway.Application;
using Bitai.LDAPGateway.Infrastructure;
using Bitai.LDAPGateway.Api.Middleware;
using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Api.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(opts =>
{
    // Dynamic (runtime compilation, needs WolverineFx.RuntimeCompilation) in Development;
    // pre-generated code in Production (dotnet run -- codegen write; generated files are committed).
    opts.Services.CritterStackDefaults(x =>
    {
        x.Production.GeneratedCodeMode = TypeLoadMode.Static;
        x.Production.AssertAllPreGeneratedTypesExist = true;
    });

    // Handlers live outside the host assembly: Application (commands/queries) and Infrastructure (notification handler).
    opts.Discovery.IncludeAssembly(typeof(Bitai.LDAPGateway.Application.DependencyInjection).Assembly);
    opts.Discovery.IncludeAssembly(typeof(Bitai.LDAPGateway.Infrastructure.DependencyInjection).Assembly);

    // Replaces a FluentValidation ValidationBehavior (throws FluentValidation.ValidationException).
    // Remove services.AddValidatorsFromAssembly(...): UseFluentValidation registers the validators.
    opts.UseFluentValidation();

    // Result-aware logging (see ResultLoggingMiddleware / ResultLoggingPolicy templates).
    opts.Policies.AddMiddleware(typeof(ResultLoggingMiddleware),
        chain => chain.MessageType.Namespace!.StartsWith("Bitai.LDAPGateway.Application"));
    opts.Policies.Add<ResultLoggingPolicy>();
});

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
