using Microsoft.AspNetCore.Routing.Patterns;
using Bitai.LDAPGateway.Application.Common.Interfaces;

namespace Bitai.LDAPGateway.Api.Controllers.ParameterPolicies;

public sealed class LdapCatalogTypeParameterPolicy : IRouteConstraint
{
    public LdapCatalogTypeParameterPolicy()
    {
        // Constructor logic here if needed
    }

    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var routeValue))
            return false;

        var profileId = routeValue as string;
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        if (!Enum.TryParse<Domain.Enums.CatalogType>(profileId, true, out var resolvedCatalogType))
            return false;

        return true;
    }
}
