using Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Enums;
using Moq;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class AuthenticateCommandHandlerTests
{
   [Fact]
   public async Task Handle_ValidRequest_ShouldReturnSuccessAndPublishEvent()
   {
      var ldapClientMock = new Mock<ILdapGatewayClient>();
      var publisherMock = new Mock<IDomainEventPublisher>();

      ldapClientMock
         .Setup(x => x.AuthenticateAsync(It.IsAny<LdapRequestContext>(), "john", "pwd", It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<AuthenticationResultDto>.Success(new AuthenticationResultDto(true, "john", "ok")));

      var handler = new AuthenticateCommandHandler(ldapClientMock.Object, publisherMock.Object);

      var result = await handler.Handle(new AuthenticateCommand("EDU", CatalogType.LC, "john", "pwd"), CancellationToken.None);

      Assert.True(result.IsSuccess);

      publisherMock.Verify(
         x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
         Times.Once);
   }
}
