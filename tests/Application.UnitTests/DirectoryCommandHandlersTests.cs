using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Commands.CreateMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.DeleteMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.DisableMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.SetMsAdUserPassword;
using Bitai.LDAPGateway.Domain.Enums;
using Moq;
using static Bitai.LDAPGateway.Application.UnitTests.DirectoryTestSupport;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class DirectoryCommandHandlersTests
{
   private const string Secret = "LeakCheck-9f3a!";

   private static CreateMsAdUserCommand NewCreateCommand(string profile = Profile, CatalogType catalog = Catalog) =>
      new(profile, catalog)
      {
         DistinguishedNameOfContainer = "OU=Users,DC=ie,DC=edu,DC=pe",
         DistinguishedName = "CN=John,OU=Users,DC=ie,DC=edu,DC=pe",
         Cn = "John",
         DisplayName = "John Doe",
         SAMAccountName = "jdoe",
         ObjectClass = ["user"],
         Password = Secret
      };

   // CreateMsAdUser

   [Fact]
   public async Task CreateMsAdUser_Success_ForwardsContextAndPayload_AndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.CreateMsAdUserAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            It.Is<CreateMsAdUserDto>(d => d.SAMAccountName == "jdoe" && d.Password == Secret && d.DistinguishedNameOfContainer == "OU=Users,DC=ie,DC=edu,DC=pe"),
            It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto>.Success(Entry()));

      var result = await new CreateMsAdUserCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(NewCreateCommand(), CancellationToken.None);

      Assert.True(result.IsSuccess);
      s.VerifyEvent("CreateMsAdUser", true);
   }

   [Fact]
   public async Task CreateMsAdUser_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.CreateMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<CreateMsAdUserDto>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto>.Failure(Failure));

      var result = await new CreateMsAdUserCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(NewCreateCommand(), CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal("bad_gateway", result.Error!.Code);
      s.VerifyEvent("CreateMsAdUser", false);
   }

   [Fact]
   public void CreateMsAdUserValidator_AcceptsGlobalCatalogAndRejectsLocalCatalogAndBlankProfile()
   {
      var validator = new CreateMsAdUserCommandValidator();

      Assert.True(validator.Validate(NewCreateCommand()).IsValid);
      Assert.False(validator.Validate(NewCreateCommand(catalog: CatalogType.LC)).IsValid);
      Assert.False(validator.Validate(NewCreateCommand(profile: " ")).IsValid);
   }

   // SetMsAdUserPassword

   [Fact]
   public async Task SetMsAdUserPassword_Success_ForwardsArguments_AndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SetMsAdUserPasswordAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            LdapIdentifierAttribute.SAMAccountName, "jdoe", Secret, true, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result.Success());

      var result = await new SetMsAdUserPasswordCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new SetMsAdUserPasswordCommand(Profile, Catalog, LdapIdentifierAttribute.SAMAccountName, "jdoe", Secret, true), CancellationToken.None);

      Assert.True(result.IsSuccess);
      s.VerifyEvent("SetMsAdUserPassword", true);
   }

   [Fact]
   public async Task SetMsAdUserPassword_ConnectorFailure_IsReturnedAndPublishedAsFailed_WithoutPasswordInEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SetMsAdUserPasswordAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result.Failure(Failure));

      var result = await new SetMsAdUserPasswordCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new SetMsAdUserPasswordCommand(Profile, Catalog, LdapIdentifierAttribute.SAMAccountName, "jdoe", Secret, false), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("SetMsAdUserPassword", false);
      s.Publisher.Verify(p => p.PublishAsync(
         It.Is<Bitai.LDAPGateway.Domain.Abstractions.IDomainEvent>(e => e.ToString()!.Contains(Secret)), It.IsAny<CancellationToken>()), Times.Never);
   }

   [Theory]
   [InlineData("", "jdoe", "pwd")]
   [InlineData("EDU", " ", "pwd")]
   [InlineData("EDU", "jdoe", "")]
   public void SetMsAdUserPasswordValidator_RejectsBlankFields(string profile, string identifier, string password)
   {
      var command = new SetMsAdUserPasswordCommand(profile, Catalog, LdapIdentifierAttribute.SAMAccountName, identifier, password, false);

      Assert.False(new SetMsAdUserPasswordCommandValidator().Validate(command).IsValid);
   }

   // DisableMsAdUser

   [Fact]
   public async Task DisableMsAdUser_Success_ForwardsArgumentsIncludingReason_AndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.DisableMsAdUserAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            LdapIdentifierAttribute.DistinguishedName, "CN=John", "left company", It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result.Success());

      var result = await new DisableMsAdUserCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new DisableMsAdUserCommand(Profile, Catalog, LdapIdentifierAttribute.DistinguishedName, "CN=John", "left company"), CancellationToken.None);

      Assert.True(result.IsSuccess);
      s.VerifyEvent("DisableMsAdUser", true);
   }

   [Fact]
   public async Task DisableMsAdUser_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.DisableMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result.Failure(Failure));

      var result = await new DisableMsAdUserCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new DisableMsAdUserCommand(Profile, Catalog, LdapIdentifierAttribute.SAMAccountName, "jdoe", null), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("DisableMsAdUser", false);
   }

   [Theory]
   [InlineData("", "jdoe")]
   [InlineData("EDU", "")]
   public void DisableMsAdUserValidator_RejectsBlankFields(string profile, string identifier)
   {
      var command = new DisableMsAdUserCommand(profile, Catalog, LdapIdentifierAttribute.SAMAccountName, identifier, null);

      Assert.False(new DisableMsAdUserCommandValidator().Validate(command).IsValid);
   }

   // DeleteMsAdUser

   [Fact]
   public async Task DeleteMsAdUser_Success_ForwardsArguments_AndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.DeleteMsAdUserAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            LdapIdentifierAttribute.SAMAccountName, "jdoe", It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result.Success());

      var result = await new DeleteMsAdUserCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new DeleteMsAdUserCommand(Profile, Catalog, LdapIdentifierAttribute.SAMAccountName, "jdoe"), CancellationToken.None);

      Assert.True(result.IsSuccess);
      s.VerifyEvent("DeleteMsAdUser", true);
   }

   [Fact]
   public async Task DeleteMsAdUser_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.DeleteMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result.Failure(Failure));

      var result = await new DeleteMsAdUserCommandHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new DeleteMsAdUserCommand(Profile, Catalog, LdapIdentifierAttribute.SAMAccountName, "jdoe"), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("DeleteMsAdUser", false);
   }

   [Theory]
   [InlineData("", "jdoe")]
   [InlineData("EDU", " ")]
   public void DeleteMsAdUserValidator_RejectsBlankFields(string profile, string identifier)
   {
      var command = new DeleteMsAdUserCommand(profile, Catalog, LdapIdentifierAttribute.SAMAccountName, identifier);

      Assert.False(new DeleteMsAdUserCommandValidator().Validate(command).IsValid);
   }
}
