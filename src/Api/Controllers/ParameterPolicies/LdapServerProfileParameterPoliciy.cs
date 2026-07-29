using System.Reflection.Metadata;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Bitai.LDAPGateway.Api.Controllers.ParameterPolicies;

public sealed class LdapServerProfileParameterPolicy : IRouteConstraint
{
    private readonly IServerProfileReadService _ldapServerProfileReader;

    public LdapServerProfileParameterPolicy(IServerProfileReadService ldapServerProfileReader)
    {
        _ldapServerProfileReader = ldapServerProfileReader;
    }    

    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var routeValue))
            return false;

        var profileId = routeValue as string;
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        return _ldapServerProfileReader.GetProfileIds().Any(m =>
            m.Equals(profileId, StringComparison.OrdinalIgnoreCase));
    }
}
