using Bitai.LDAPGateway.Application.Common.Behaviors;
using JasperFx;
using JasperFx.CodeGeneration;
using Wolverine;
using Wolverine.FluentValidation;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ConfigureHostBuilderExtensions
{
    public static IHostBuilder UseWolverine(this ConfigureHostBuilder host)
    {
        return host.UseWolverine(opts =>
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
    }
}
