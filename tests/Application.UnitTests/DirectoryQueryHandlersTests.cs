using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Queries.GetDirectoryEntryByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupParents;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserParents;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchDirectory;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchGroups;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchUsers;
using Bitai.LDAPGateway.Domain.Enums;
using Moq;
using static Bitai.LDAPGateway.Application.UnitTests.DirectoryTestSupport;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class DirectoryQueryHandlersTests
{
   private const LdapIdentifierAttribute IdAttr = LdapIdentifierAttribute.SAMAccountName;
   private const LdapEntryAttributeSet Set = LdapEntryAttributeSet.Minimum;

   // Searches

   [Fact]
   public async Task SearchDirectory_Success_ForwardsFiltersAndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SearchDirectoryAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            LdapEntryAttribute.cn, "john*", LdapEntryAttribute.mail, "*@edu.pe", true, Set, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Success(Entries("jdoe")));

      var result = await new SearchDirectoryQueryHandler(s.Connector.Object, s.Publisher.Object).Handle(
         new SearchDirectoryQuery(Profile, Catalog, LdapEntryAttribute.cn, "john*", LdapEntryAttribute.mail, "*@edu.pe", true, Set),
         CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Single(result.Value!);
      s.VerifyEvent("SearchDirectory", true);
   }

   [Fact]
   public async Task SearchDirectory_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SearchDirectoryAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapEntryAttribute>(), It.IsAny<string>(),
            It.IsAny<LdapEntryAttribute?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Failure(Failure));

      var result = await new SearchDirectoryQueryHandler(s.Connector.Object, s.Publisher.Object).Handle(
         new SearchDirectoryQuery(Profile, Catalog, LdapEntryAttribute.cn, "x", null, null, null, Set), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("SearchDirectory", false);
   }

   [Fact]
   public async Task SearchUsers_Success_ForwardsFiltersAndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SearchUsersAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            LdapEntryAttribute.cn, "john*", null, null, null, Set, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Success(Entries("jdoe", "jroe")));

      var result = await new SearchUsersQueryHandler(s.Connector.Object, s.Publisher.Object).Handle(
         new SearchUsersQuery(Profile, Catalog, LdapEntryAttribute.cn, "john*", null, null, null, Set), CancellationToken.None);

      Assert.True(result.IsSuccess);
      Assert.Equal(2, result.Value!.Count);
      s.VerifyEvent("SearchUsers", true);
   }

   [Fact]
   public async Task SearchUsers_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SearchUsersAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapEntryAttribute>(), It.IsAny<string>(),
            It.IsAny<LdapEntryAttribute?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Failure(Failure));

      var result = await new SearchUsersQueryHandler(s.Connector.Object, s.Publisher.Object).Handle(
         new SearchUsersQuery(Profile, Catalog, LdapEntryAttribute.cn, "x", null, null, null, Set), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("SearchUsers", false);
   }

   [Fact]
   public async Task SearchGroups_Success_ForwardsFiltersAndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SearchGroupsAsync(It.Is<LdapRequestContext>(x => IsContext(x)),
            LdapEntryAttribute.cn, "admins*", null, null, null, Set, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Success(Entries("admins")));

      var result = await new SearchGroupsQueryHandler(s.Connector.Object, s.Publisher.Object).Handle(
         new SearchGroupsQuery(Profile, Catalog, LdapEntryAttribute.cn, "admins*", null, null, null, Set), CancellationToken.None);

      Assert.True(result.IsSuccess);
      s.VerifyEvent("SearchGroups", true);
   }

   [Fact]
   public async Task SearchGroups_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.SearchGroupsAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapEntryAttribute>(), It.IsAny<string>(),
            It.IsAny<LdapEntryAttribute?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Failure(Failure));

      var result = await new SearchGroupsQueryHandler(s.Connector.Object, s.Publisher.Object).Handle(
         new SearchGroupsQuery(Profile, Catalog, LdapEntryAttribute.cn, "x", null, null, null, Set), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("SearchGroups", false);
   }

   [Fact]
   public void SearchValidators_RequireFilterValue_AndSecondFilterAllOrNothing()
   {
      var valid = new SearchUsersQuery(Profile, Catalog, LdapEntryAttribute.cn, "john", null, null, null, Set);
      var blankValue = valid with { FilterValue = "" };
      var secondAttributeOnly = valid with { SecondFilterAttribute = LdapEntryAttribute.mail };
      var badEnum = valid with { RequiredAttributeSet = (LdapEntryAttributeSet)999 };

      var validator = new SearchUsersQueryValidator();
      Assert.True(validator.Validate(valid).IsValid);
      Assert.False(validator.Validate(blankValue).IsValid);
      Assert.False(validator.Validate(secondAttributeOnly).IsValid);
      Assert.False(validator.Validate(badEnum).IsValid);

      var groupsValidator = new SearchGroupsQueryValidator();
      Assert.False(groupsValidator.Validate(new SearchGroupsQuery(Profile, Catalog, LdapEntryAttribute.cn, "", null, null, null, Set)).IsValid);

      var directoryValidator = new SearchDirectoryQueryValidator();
      Assert.False(directoryValidator.Validate(new SearchDirectoryQuery(Profile, Catalog, LdapEntryAttribute.cn, "", null, null, null, Set)).IsValid);
   }

   // Single entry lookups

   [Fact]
   public async Task GetUserByIdentifier_Found_ReturnsEntryAndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetUserAsync(It.Is<LdapRequestContext>(x => IsContext(x)), IdAttr, "jdoe", Set, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Success(Entry()));

      var result = await new GetUserByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetUserByIdentifierQuery(Profile, Catalog, IdAttr, "jdoe", Set, true), CancellationToken.None);

      Assert.Equal("jdoe", result.Value!.samAccountName);
      s.VerifyEvent("GetUserByIdentifier", true);
   }

   [Theory]
   [InlineData(true, false)]
   [InlineData(false, true)]
   public async Task GetUserByIdentifier_NotFound_DependsOnMustExist(bool mustExist, bool expectedSuccess)
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Success(null));

      var result = await new GetUserByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetUserByIdentifierQuery(Profile, Catalog, IdAttr, "ghost", Set, mustExist), CancellationToken.None);

      Assert.Equal(expectedSuccess, result.IsSuccess);
      if (!expectedSuccess)
      {
         Assert.Equal("not_found", result.Error!.Code);
      }
      else
      {
         Assert.Null(result.Value);
      }
   }

   [Fact]
   public async Task GetGroupByIdentifier_Found_ReturnsEntryAndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetGroupAsync(It.Is<LdapRequestContext>(x => IsContext(x)), IdAttr, "admins", Set, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Success(Entry("admins")));

      var result = await new GetGroupByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetGroupByIdentifierQuery(Profile, Catalog, IdAttr, "admins", Set, true), CancellationToken.None);

      Assert.Equal("admins", result.Value!.samAccountName);
      s.VerifyEvent("GetGroupByIdentifier", true);
   }

   [Theory]
   [InlineData(true, false)]
   [InlineData(false, true)]
   public async Task GetGroupByIdentifier_NotFound_DependsOnMustExist(bool mustExist, bool expectedSuccess)
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetGroupAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Success(null));

      var result = await new GetGroupByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetGroupByIdentifierQuery(Profile, Catalog, IdAttr, "ghost", Set, mustExist), CancellationToken.None);

      Assert.Equal(expectedSuccess, result.IsSuccess);
      Assert.Equal(!expectedSuccess ? "not_found" : null, result.Error?.Code);
   }

   [Fact]
   public async Task GetDirectoryEntryByIdentifier_Found_ReturnsEntryAndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetDirectoryEntryAsync(It.Is<LdapRequestContext>(x => IsContext(x)), IdAttr, "jdoe", Set, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Success(Entry()));

      var result = await new GetDirectoryEntryByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetDirectoryEntryByIdentifierQuery(Profile, Catalog, "jdoe", IdAttr, Set, true), CancellationToken.None);

      Assert.Equal("jdoe", result.Value!.samAccountName);
      s.VerifyEvent("GetDirectoryEntryByIdentifier", true);
   }

   [Theory]
   [InlineData(true, false)]
   [InlineData(false, true)]
   public async Task GetDirectoryEntryByIdentifier_NotFound_DependsOnMustExist(bool mustExist, bool expectedSuccess)
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetDirectoryEntryAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Success(null));

      var result = await new GetDirectoryEntryByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetDirectoryEntryByIdentifierQuery(Profile, Catalog, "ghost", IdAttr, Set, mustExist), CancellationToken.None);

      Assert.Equal(expectedSuccess, result.IsSuccess);
      Assert.Equal(!expectedSuccess ? "not_found" : null, result.Error?.Code);
   }

   [Fact]
   public async Task GetUserByIdentifier_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<LdapEntryDto?>.Failure(Failure));

      var result = await new GetUserByIdentifierQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetUserByIdentifierQuery(Profile, Catalog, IdAttr, "jdoe", Set, true), CancellationToken.None);

      Assert.Equal("bad_gateway", result.Error!.Code);
      s.VerifyEvent("GetUserByIdentifier", false);
   }

   // Parents

   [Fact]
   public async Task GetUserParents_Success_ForwardsMustExistFlag_AndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetUserParentsAsync(It.Is<LdapRequestContext>(x => IsContext(x)), "jdoe", IdAttr, Set, true, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Success(Entries("staff")));

      var result = await new GetUserParentsQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetUserParentsQuery(Profile, Catalog, "jdoe", IdAttr, Set, true), CancellationToken.None);

      Assert.Single(result.Value!);
      s.VerifyEvent("GetUserParents", true);
   }

   [Fact]
   public async Task GetUserParents_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetUserParentsAsync(It.IsAny<LdapRequestContext>(), It.IsAny<string>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.NotFound("user not found")));

      var result = await new GetUserParentsQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetUserParentsQuery(Profile, Catalog, "ghost", IdAttr, Set, true), CancellationToken.None);

      Assert.Equal("not_found", result.Error!.Code);
      s.VerifyEvent("GetUserParents", false);
   }

   [Fact]
   public async Task GetGroupParents_Success_ForwardsMustExistFlag_AndPublishesEvent()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetGroupParentsAsync(It.Is<LdapRequestContext>(x => IsContext(x)), "admins", IdAttr, Set, false, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Success(Entries("all-staff")));

      var result = await new GetGroupParentsQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetGroupParentsQuery(Profile, Catalog, "admins", IdAttr, Set, false), CancellationToken.None);

      Assert.Single(result.Value!);
      s.VerifyEvent("GetGroupParents", true);
   }

   [Fact]
   public async Task GetGroupParents_ConnectorFailure_IsReturnedAndPublishedAsFailed()
   {
      var s = new DirectoryTestSupport();
      s.Connector
         .Setup(c => c.GetGroupParentsAsync(It.IsAny<LdapRequestContext>(), It.IsAny<string>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Failure(Failure));

      var result = await new GetGroupParentsQueryHandler(s.Connector.Object, s.Publisher.Object)
         .Handle(new GetGroupParentsQuery(Profile, Catalog, "admins", IdAttr, Set, true), CancellationToken.None);

      Assert.False(result.IsSuccess);
      s.VerifyEvent("GetGroupParents", false);
   }

   // Lookup validators

   [Fact]
   public void LookupValidators_RejectBlankProfileOrIdentifier_AndInvalidEnums()
   {
      Assert.True(new GetUserByIdentifierQueryValidator().Validate(new GetUserByIdentifierQuery(Profile, Catalog, IdAttr, "jdoe", Set, true)).IsValid);
      Assert.False(new GetUserByIdentifierQueryValidator().Validate(new GetUserByIdentifierQuery("", Catalog, IdAttr, "jdoe", Set, true)).IsValid);
      Assert.False(new GetGroupByIdentifierQueryValidator().Validate(new GetGroupByIdentifierQuery(Profile, Catalog, IdAttr, " ", Set, true)).IsValid);
      Assert.False(new GetDirectoryEntryByIdentifierQueryValidator().Validate(new GetDirectoryEntryByIdentifierQuery(Profile, (CatalogType)99, "x", IdAttr, Set, true)).IsValid);
      Assert.False(new GetUserParentsQueryValidator().Validate(new GetUserParentsQuery(Profile, Catalog, "x", (LdapIdentifierAttribute)99, Set, true)).IsValid);
      Assert.False(new GetGroupParentsQueryValidator().Validate(new GetGroupParentsQuery(Profile, Catalog, "", IdAttr, Set, true)).IsValid);
   }
}
