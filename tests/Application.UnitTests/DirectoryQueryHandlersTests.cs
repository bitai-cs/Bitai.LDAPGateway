using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Queries.GetDirectoryEntryByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchDirectory;
using Bitai.LDAPGateway.Domain.Enums;
using Moq;
using static Bitai.LDAPGateway.Application.UnitTests.DirectoryTestSupport;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class DirectoryQueryHandlersTests
{
   private const LdapIdentifierAttribute IdentifierAttribute = LdapIdentifierAttribute.SAMAccountName;
   private const LdapEntryAttributeSet AttributeSet = LdapEntryAttributeSet.Minimum;

   [Fact]
   public void SearchDirectoryValidator_AcceptsZeroValuedDefinedFilterAttribute()
   {
      var query = new SearchDirectoryQuery(Profile, Catalog, LdapEntryAttribute.c, "PE", null, null, null, AttributeSet);

      Assert.True(new SearchDirectoryQueryValidator().Validate(query).IsValid);
   }

   [Fact]
   public void SearchDirectoryValidator_RejectsUndefinedPrimaryFilterAttribute()
   {
      var query = new SearchDirectoryQuery(Profile, Catalog, (LdapEntryAttribute)999, "value", null, null, null, AttributeSet);

      Assert.False(new SearchDirectoryQueryValidator().Validate(query).IsValid);
   }

   [Fact]
   public void SearchDirectoryValidator_RequiresSecondaryFilterFieldsAsCompleteSet()
   {
      var valid = new SearchDirectoryQuery(Profile, Catalog, LdapEntryAttribute.cn, "john", null, null, null, AttributeSet);
      var validator = new SearchDirectoryQueryValidator();

      Assert.False(validator.Validate(valid with { SecondFilterAttribute = LdapEntryAttribute.mail }).IsValid);
      Assert.False(validator.Validate(valid with { SecondFilterValue = "john@example.test" }).IsValid);
      Assert.False(validator.Validate(valid with { CombineFilters = true }).IsValid);
      Assert.False(validator.Validate(valid with
      {
         SecondFilterAttribute = LdapEntryAttribute.mail,
         CombineFilters = true
      }).IsValid);
      Assert.False(validator.Validate(valid with
      {
         SecondFilterAttribute = LdapEntryAttribute.mail,
         SecondFilterValue = "john@example.test"
      }).IsValid);
      Assert.True(validator.Validate(valid with
      {
         SecondFilterAttribute = LdapEntryAttribute.mail,
         SecondFilterValue = "john@example.test",
         CombineFilters = true
      }).IsValid);
   }

   [Fact]
   public async Task GetGroupByIdentifier_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var support = new DirectoryTestSupport();
      support.Connector
         .Setup(c => c.GetGroupAsync(
            It.IsAny<LdapRequestContext>(),
            It.IsAny<LdapIdentifierAttribute>(),
            It.IsAny<string>(),
            It.IsAny<LdapEntryAttributeSet>(),
            It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Failure(Failure));

      var result = await new GetGroupByIdentifierQueryHandler(support.Connector.Object, support.Publisher.Object)
         .Handle(new GetGroupByIdentifierQuery(Profile, Catalog, IdentifierAttribute, "admins", AttributeSet, true), CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal(Failure.Code, result.Error!.Code);
      support.VerifyEvent("GetGroupByIdentifier", false);
   }

   [Fact]
   public async Task GetDirectoryEntryByIdentifier_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var support = new DirectoryTestSupport();
      support.Connector
         .Setup(c => c.GetDirectoryEntryAsync(
            It.IsAny<LdapRequestContext>(),
            It.IsAny<LdapIdentifierAttribute>(),
            It.IsAny<string>(),
            It.IsAny<LdapEntryAttributeSet>(),
            It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Failure(Failure));

      var result = await new GetDirectoryEntryByIdentifierQueryHandler(support.Connector.Object, support.Publisher.Object)
         .Handle(new GetDirectoryEntryByIdentifierQuery(Profile, Catalog, "jdoe", IdentifierAttribute, AttributeSet, true), CancellationToken.None);

      Assert.False(result.IsSuccess);
      Assert.Equal(Failure.Code, result.Error!.Code);
      support.VerifyEvent("GetDirectoryEntryByIdentifier", false);
   }
}
