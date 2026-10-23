using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

public sealed class EndpointAuthorizationMetadataTests
{
    [Fact]
    public void EveryControllerEndpoint_HasAnExplicitAuthorizationPolicy()
    {
        var unprotectedActions = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(method => new
            {
                Method = method,
                Metadata = method.DeclaringType!
                    .GetCustomAttributes()
                    .Concat(method.GetCustomAttributes())
                    .ToArray(),
            })
            .Where(action => action.Metadata.OfType<IActionHttpMethodProvider>().Any()
                || action.Metadata.OfType<IRouteTemplateProvider>().Any())
            .Where(action => action.Metadata.OfType<IAllowAnonymous>().Any()
                || !action.Metadata
                    .OfType<IAuthorizeData>()
                    .Any(authorization => !string.IsNullOrWhiteSpace(authorization.Policy)))
            .Select(action => $"{action.Method.DeclaringType!.Name}.{action.Method.Name}")
            .ToArray();

        Assert.Empty(unprotectedActions);
    }
}
