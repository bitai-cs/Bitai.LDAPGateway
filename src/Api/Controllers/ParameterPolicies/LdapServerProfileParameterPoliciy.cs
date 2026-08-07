using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;
using MediatR;

namespace Bitai.LDAPGateway.Api.Controllers.ParameterPolicies;

public sealed class LdapServerProfileParameterPolicy : IRouteConstraint
{
    private readonly IMediator _mediator;

    public LdapServerProfileParameterPolicy(IMediator mediator)
    {
        _mediator = mediator;
    }    

    bool IRouteConstraint.Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var routeValue))
            return false;

        var profileId = routeValue as string;
        if (string.IsNullOrWhiteSpace(profileId))
            return false;

        Result<IReadOnlyList<string>> result = _mediator.Send(new GetProfileIdsQuery()).GetAwaiter().GetResult();
        if (!result.IsSuccess)
            return false;

        if (result.Value == null || !result.Value.Contains(profileId))
            return false;

        return true;
    }
}
