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
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Where(method => !method.GetCustomAttributes()
                .OfType<IAuthorizeData>()
                .Any(authorization => !string.IsNullOrWhiteSpace(authorization.Policy)))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(unprotectedActions);
    }
}
