using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Bitai.LDAPGateway.Application.Common.Models;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace Bitai.LDAPGateway.Application.Common.Behaviors;

/// <summary>
/// Feeds each Application handler's own return variable (Result or Result&lt;T&gt;) to
/// ResultLoggingMiddleware.LogResult. Verified on Wolverine 6.44; check the namespaces/signature on other versions.
/// </summary>
public sealed class ResultLoggingPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(c => c.MessageType.Namespace!.StartsWith("Bitai.LDAPGateway.Application")))
        {
            var rv = chain.Handlers.Last().ReturnVariable;
            if (rv is null || !typeof(Result).IsAssignableFrom(rv.VariableType)) continue;

            var call = new MethodCall(typeof(ResultLoggingMiddleware), nameof(ResultLoggingMiddleware.LogResult));
            call.Arguments[0] = rv;               // Result<T> passed to a Result parameter: plain C# conversion
            chain.Postprocessors.Add(call);
        }
    }
}
