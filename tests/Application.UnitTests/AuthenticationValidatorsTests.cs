using Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;
using Bitai.LDAPGateway.Application.Authentications.Commands.AuthenticateWithoutUserLookup;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class AuthenticationValidatorsTests
{
   public static TheoryData<string?> InvalidPasswords => new() { null, "", " ", "   " };

   [Theory]
   [MemberData(nameof(InvalidPasswords))]
   public void Authenticate_RejectsEmptyOrBlankPassword(string? password)
   {
      var command = new AuthenticateCommand("EDU", CatalogType.LC, new UserCredential("jdoe", new Secret(password!)));

      var result = new AuthenticateCommandValidator().Validate(command);

      Assert.Contains(result.Errors, e => e.PropertyName == "Password");
   }

   [Fact]
   public void Authenticate_RejectsDefaultSecret()
   {
      var command = new AuthenticateCommand("EDU", CatalogType.LC, new UserCredential("jdoe", default));

      Assert.False(new AuthenticateCommandValidator().Validate(command).IsValid);
   }

   [Fact]
   public void Authenticate_AcceptsValidPassword()
   {
      var command = new AuthenticateCommand("EDU", CatalogType.LC, new UserCredential("jdoe", new Secret("p4ss")));

      Assert.True(new AuthenticateCommandValidator().Validate(command).IsValid);
   }

   [Theory]
   [MemberData(nameof(InvalidPasswords))]
   public void AuthenticateWithoutUserLookup_RejectsEmptyOrBlankPassword(string? password)
   {
      var command = new AuthenticateWithoutUserLookupCommand("EDU", CatalogType.LC, new UserCredential("jdoe", new Secret(password!)));

      var result = new AuthenticateWithoutUserLookupCommandValidator().Validate(command);

      Assert.Contains(result.Errors, e => e.PropertyName == "Password");
   }

   [Fact]
   public void AuthenticateWithoutUserLookup_RejectsDefaultSecret()
   {
      var command = new AuthenticateWithoutUserLookupCommand("EDU", CatalogType.LC, new UserCredential("jdoe", default));

      Assert.False(new AuthenticateWithoutUserLookupCommandValidator().Validate(command).IsValid);
   }

   [Fact]
   public void AuthenticateWithoutUserLookup_AcceptsValidPassword()
   {
      var command = new AuthenticateWithoutUserLookupCommand("EDU", CatalogType.LC, new UserCredential("jdoe", new Secret("p4ss")));

      Assert.True(new AuthenticateWithoutUserLookupCommandValidator().Validate(command).IsValid);
   }
}
