using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;
using MediatR;

namespace Bitai.LDAPGateway.Api.Controllers.RouteConstraints;

public sealed class DirectoryUserEntryRouteConstraint : IRouteConstraint
{
    private readonly Application.Common.Interfaces.IDirectoryServiceConnector _directoryConnector;

    public DirectoryUserEntryRouteConstraint(Application.Common.Interfaces.IDirectoryServiceConnector directoryConnector)
    {
        _directoryConnector = directoryConnector;
    }

    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (httpContext is null)
            return false;

        // Check if the Directory Server Profile route value exists
        if (!values.TryGetValue("serverProfile", out var serverProfileObj))
            return false;

        var serverProfile = serverProfileObj as string;
        if (string.IsNullOrEmpty(serverProfile))
            return false;

        // Check if the Directory Catalog Type route value exists  
        if (!values.TryGetValue("catalogType", out var categoryTypeObj))
            return false;

        var categoryType = categoryTypeObj as string;
        if (string.IsNullOrEmpty(categoryType))
            return false;

        if (!Enum.TryParse<Domain.Enums.CatalogType>(categoryType, true, out var resolvedCatalogType))
            return false;

        // Check if the Directory User Identifier route value exists
        if (!values.TryGetValue(routeKey, out var userIdentifierObj))
            return false;
       
        var userIdentifier = userIdentifierObj as string;
        if (string.IsNullOrWhiteSpace(userIdentifier))
            return false;

        var identifierAttributeName = httpContext.Request.Query["IdentifierAttribute"].ToString();
        var identifierAttribute = Enum.Parse<Domain.Enums.LdapIdentifierAttribute>(identifierAttributeName, true);

        var context = new LdapRequestContext(serverProfile, resolvedCatalogType);

        // Check if the Directory User Identifier exists in the Directory Server Profile and Catalog Type
        var result = _directoryConnector.GetUserAsync(context, identifierAttribute, userIdentifier, Domain.Enums.LdapEntryAttributeSet.Minimum, default).GetAwaiter().GetResult();

        // If the query failed or the user was not found, return false
        if (!result.IsSuccess || result.Value == null)
            return false;
        
        return true;
    }
    //public static bool IsDistinguishedName(ReadOnlySpan<char> value)
    //{
    //    value = value.Trim();

    //    int index = value.IndexOf('=');

    //    return index > 0 && index < value.Length - 1;
    //}
}
