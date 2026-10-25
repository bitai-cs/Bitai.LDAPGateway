using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Commands.CreateMsAdUser;
using Bitai.LDAPGateway.Domain.Enums;
using Moq;
using static Bitai.LDAPGateway.Application.UnitTests.DirectoryTestSupport;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class DirectoryCommandHandlersTests
{
   private const string Password = "TestOnly-9f3a!";

   [Fact]
   public async Task CreateMsAdUser_Success_ForwardsContextAndPayload_ReturnsConnectorEntryAndPublishesEvent()
   {
      var support = new DirectoryTestSupport();
      var createdEntry = Entry("created-jdoe");
      var command = new CreateMsAdUserCommand(Profile, Catalog)
      {
         DistinguishedNameOfContainer = "OU=Users,DC=ie,DC=edu,DC=pe",
         DistinguishedName = "CN=John,OU=Users,DC=ie,DC=edu,DC=pe",
         Cn = "John",
         DisplayName = "John Doe",
         SAMAccountName = "jdoe",
         ObjectClass = ["user"],
         Password = Password
      };

      support.Connector
         .Setup(c => c.CreateMsAdUserAsync(
            It.Is<LdapRequestContext>(context => IsContext(context)),
            It.Is<CreateMsAdUserDto>(request =>
               request.SAMAccountName == command.SAMAccountName
               && request.Password == command.Password
               && request.DistinguishedNameOfContainer == command.DistinguishedNameOfContainer),
            It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto>.Success(createdEntry));

      var result = await new CreateMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object)
         .Handle(command, CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Same(createdEntry, result.Value);
      support.Connector.Verify(
         c => c.CreateMsAdUserAsync(
            It.Is<LdapRequestContext>(context => IsContext(context)),
            It.Is<CreateMsAdUserDto>(request =>
               request.SAMAccountName == command.SAMAccountName
               && request.Password == command.Password
               && request.DistinguishedNameOfContainer == command.DistinguishedNameOfContainer),
            It.IsAny<CancellationToken>()),
         Times.Once);
      support.VerifyEvent("CreateMsAdUser", true);
   }
}
