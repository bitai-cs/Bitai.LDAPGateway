namespace Bitai.LDAPGateway.Api.Controllers.RouteConstraints;

public sealed class DirectoryCatalogTypeRouteConstraint : IRouteConstraint
{
    public DirectoryCatalogTypeRouteConstraint()
    {
        // Constructor logic here if needed
    }

    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var routeValue))
            return false;

        var catalogType = routeValue as string;
        if (string.IsNullOrWhiteSpace(catalogType))
            return false;

        if (!Enum.TryParse<Domain.Enums.CatalogType>(catalogType, true, out var resolvedCatalogType))
            return false;

        return true;
    }
}
