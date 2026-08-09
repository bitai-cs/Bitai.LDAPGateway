namespace Bitai.LDAPGateway.Api.Controllers.RouteConstraints;

public sealed class DirectoryGroupRouteConstraint : IRouteConstraint
{
    private readonly Application.Common.Interfaces.IDirectoryServiceConnector _directoryConnector;



    public DirectoryGroupRouteConstraint(Application.Common.Interfaces.IDirectoryServiceConnector directoryConnector)
    {
        _directoryConnector = directoryConnector;
    }    



    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (httpContext == null)
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

        if (!Enum.TryParse<Domain.Enums.CatalogType>(categoryType, true, out var resolvedCategoryType))
            return false;

        // Check if the Directory User Identifier route value exists
        if (!values.TryGetValue(routeKey, out var userIdentifierObj))
            return false;
       
        var userIdentifier = userIdentifierObj as string;
        if (string.IsNullOrWhiteSpace(userIdentifier))
            return false;

        // Check if the Directory User Identifier is valid for the given Directory Server Profile
        var identifierAttributeName = httpContext.Request.Query["IdentifierAttribute"].ToString();
        var identifierAttribute = Enum.Parse<Domain.Enums.LdapIdentifierAttribute>(identifierAttributeName, true);

        var context = new Application.Common.Models.LdapRequestContext(serverProfile, resolvedCategoryType);

        // Validate the user identifier against the directory
        var result = _directoryConnector.GetGroupAsync(context, identifierAttribute, userIdentifier, Domain.Enums.LdapEntryAttributeSet.Minimum, default).GetAwaiter().GetResult();

        // If the query failed or the user was not found, return false
        if (!result.IsSuccess || result.Value == null)
            return false;
        
        return true;
    }
}
