using Bitai.LDAPGateway.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Api.Controllers.RouteConstraints;

public sealed class DirectoryServerProfileRouteConstraint : IRouteConstraint
{
    private readonly LdapServerProfilesOptions _serverProfileOptions;



    public DirectoryServerProfileRouteConstraint(IOptions<LdapServerProfilesOptions> options)
    {
        _serverProfileOptions = options.Value;
    }    



    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var routeValue))
            return false;

        var profileId = routeValue as string;
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        var exists = _serverProfileOptions.Any(x => x.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase));

        return exists;
    }
}
