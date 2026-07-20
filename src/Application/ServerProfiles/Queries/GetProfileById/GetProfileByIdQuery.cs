using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileById;

public sealed record GetProfileByIdQuery(string ProfileId) : IRequest<Result<LdapServerProfileDto>>;

public sealed class GetProfileByIdQueryValidator : AbstractValidator<GetProfileByIdQuery>
{
   public GetProfileByIdQueryValidator()
   {
      RuleFor(x => x.ProfileId).NotEmpty();
   }
}

public sealed class GetProfileByIdQueryHandler : IRequestHandler<GetProfileByIdQuery, Result<LdapServerProfileDto>>
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfileByIdQueryHandler(IServerProfileReadService serverProfileReadService)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public async Task<Result<LdapServerProfileDto>> Handle(GetProfileByIdQuery request, CancellationToken cancellationToken)
   {
      var profile = await _serverProfileReadService.GetProfileAsync(request.ProfileId, cancellationToken);
      return profile is null
         ? Result<LdapServerProfileDto>.Failure(Error.NotFound($"LDAP server profile '{request.ProfileId}' was not found."))
         : Result<LdapServerProfileDto>.Success(profile);
   }
}
