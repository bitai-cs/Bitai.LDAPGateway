using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;
using MediatR;

namespace Bitai.LDAPGateway.Api.Controllers.ParameterPolicies;

public sealed class DirectoryUserEntryParameterPolicy : IRouteConstraint
{
    private readonly IMediator _mediator;

    public DirectoryUserEntryParameterPolicy(IMediator mediator)
    {
        _mediator = mediator;
    }    

    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        // Check if the Directory Server Profile route value exists
        if (!values.TryGetValue("ldapSvrPf", out var serverProfileObj))
            return false;

        var serverProfile = serverProfileObj as string;
        if (string.IsNullOrEmpty(serverProfile))
            return false;

        // Check if the Directory Catalog Type route value exists  
        if (!values.TryGetValue("ldapCatType", out var categoryTypeObj))
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

        // Check if the Directory User Identifier is a Distinguished Name
        var isDistinguishedName = IsDistinguishedName(userIdentifier.AsSpan());
        var identifierAttribute = isDistinguishedName ? Domain.Enums.LdapIdentifierAttribute.DistinguishedName : Domain.Enums.LdapIdentifierAttribute.SAMAccountName;

        // Check if the Directory User Identifier exists in the Directory Server Profile and Catalog Type
        var result = _mediator.Send(new GetUserByIdentifierQuery(serverProfile, resolvedCategoryType, identifierAttribute, userIdentifier, Domain.Enums.LdapEntryAttributeSet.Minimum)).GetAwaiter().GetResult();

        // If the query failed or the user was not found, return false
        if (!result.IsSuccess || result.Value == null)
            return false;
        
        return true;
    }



    public static bool IsDistinguishedName(ReadOnlySpan<char> value)
    {
        value = value.Trim();

        int index = value.IndexOf('=');

        return index > 0 && index < value.Length - 1;
    }
}
