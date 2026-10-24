using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileById;

public sealed record GetProfileByIdQuery(string ProfileId);

public sealed class GetProfileByIdQueryValidator : AbstractValidator<GetProfileByIdQuery>
{
   public GetProfileByIdQueryValidator()
   {
      RuleFor(x => x.ProfileId).NotEmpty();
   }
}

/// <summary>Handles <see cref="GetProfileByIdQuery"/>.</summary>
public sealed class GetProfileByIdQueryHandler : LdapHandlerBase
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfileByIdQueryHandler(IServerProfileReadService serverProfileReadService, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public Task<Result<LdapServerProfileDto>> Handle(GetProfileByIdQuery request, CancellationToken cancellationToken)
   {
      return ExecuteWithoutCatalogAsync("GetProfileById", request.ProfileId, () =>
      {
         var profile = _serverProfileReadService.GetProfile(request.ProfileId);
         return Task.FromResult(profile is null
            ? Result<LdapServerProfileDto>.Failure(Error.NotFound($"LDAP server profile '{request.ProfileId}' was not found."))
            : Result<LdapServerProfileDto>.Success(profile));
      }, cancellationToken);
   }
}
