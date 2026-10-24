using Bitai.LDAPGateway.Application.CatalogTypes.Queries.GetCatalogTypes;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileById;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfiles;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Events;
using Moq;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class ProfileAndCatalogHandlersTests
{
   private static readonly LdapServerProfileDto Profile =
      new("EDU", "server", "389", "3268", "DC=a", "DC=a", "a.com", 30, false, false, "bind", 5);

   private readonly Mock<IServerProfileReadService> _profiles = new();
   private readonly Mock<IDomainEventPublisher> _publisher = new();
   private readonly List<OperationCompletedDomainEvent> _events = [];

   public ProfileAndCatalogHandlersTests()
   {
      _publisher
         .Setup(x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
         .Callback<IDomainEvent, CancellationToken>((e, _) => _events.Add((OperationCompletedDomainEvent)e))
         .Returns(Task.CompletedTask);
   }

   private OperationCompletedDomainEvent SingleEvent()
   {
      var ev = Assert.Single(_events);
      Assert.Null(ev.CatalogType);
      return ev;
   }

   [Fact]
   public async Task GetCatalogTypes_PublishesEvent()
   {
      var result = await new GetCatalogTypesQueryHandler(_publisher.Object).Handle(new GetCatalogTypesQuery(), CancellationToken.None);

      Assert.True(result.IsSuccess);
      var ev = SingleEvent();
      Assert.Equal("GetCatalogTypes", ev.OperationName);
      Assert.Null(ev.ServerProfile);
      Assert.True(ev.IsSuccess);
   }

   [Fact]
   public async Task GetProfileIds_PublishesEvent()
   {
      _profiles.Setup(x => x.GetProfileIds()).Returns(["EDU"]);

      var result = await new GetProfileIdsQueryHandler(_profiles.Object, _publisher.Object).Handle(new GetProfileIdsQuery(), CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("GetProfileIds", SingleEvent().OperationName);
   }

   [Fact]
   public async Task GetProfiles_PublishesEvent()
   {
      _profiles.Setup(x => x.GetProfiles()).Returns([Profile]);

      var result = await new GetProfilesQueryHandler(_profiles.Object, _publisher.Object).Handle(new GetProfilesQuery(), CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal("GetProfiles", SingleEvent().OperationName);
   }

   [Fact]
   public async Task GetProfileById_Found_PublishesSuccessEventWithProfile()
   {
      _profiles.Setup(x => x.GetProfile("EDU")).Returns(Profile);

      var result = await new GetProfileByIdQueryHandler(_profiles.Object, _publisher.Object).Handle(new GetProfileByIdQuery("EDU"), CancellationToken.None);

      Assert.True(result.IsSuccess);
      var ev = SingleEvent();
      Assert.Equal("EDU", ev.ServerProfile);
      Assert.True(ev.IsSuccess);
   }

   [Fact]
   public async Task GetProfileById_NotFound_PublishesFailureEvent()
   {
      _profiles.Setup(x => x.GetProfile("NOPE")).Returns((LdapServerProfileDto?)null);

      var result = await new GetProfileByIdQueryHandler(_profiles.Object, _publisher.Object).Handle(new GetProfileByIdQuery("NOPE"), CancellationToken.None);

      Assert.False(result.IsSuccess);
      var ev = SingleEvent();
      Assert.Equal("NOPE", ev.ServerProfile);
      Assert.False(ev.IsSuccess);
   }
}
