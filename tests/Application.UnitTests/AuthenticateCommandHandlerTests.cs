using Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;
using Moq;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class AuthenticateCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_ShouldReturnSuccessAndPublishEvent()
    {
        var ldapClientMock = new Mock<IDirectoryServiceConnector>();
        var publisherMock = new Mock<IDomainEventPublisher>();

        var credential = new UserCredential("john", new Secret("pwd"));

        ldapClientMock
           .Setup(x => x.AuthenticateAsync(It.IsAny<LdapRequestContext>(), credential, It.IsAny<CancellationToken>()))
           .ReturnsAsync(Result<AuthenticationResultDto>.Success(new AuthenticationResultDto(true, "john", "ok")));

        var handler = new AuthenticateCommandHandler(ldapClientMock.Object, publisherMock.Object);

        var result = await handler.Handle(new AuthenticateCommand("EDU", CatalogType.LC, credential), CancellationToken.None);

        Assert.True(result.IsSuccess);

        publisherMock.Verify(
           x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
           Times.Once);
    }
}
