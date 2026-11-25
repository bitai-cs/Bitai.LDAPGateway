using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Directory.Commands.CreateMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.DeleteMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.DisableMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.SetMsAdUserPassword;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchUsers;
using Bitai.LDAPGateway.Domain.Enums;
using Moq;
using static Bitai.LDAPGateway.Application.UnitTests.DirectoryTestSupport;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class DirectoryCachingTests
{
    private const LdapEntryAttributeSet Set = LdapEntryAttributeSet.Minimum;
    private const LdapIdentifierAttribute IdAttr = LdapIdentifierAttribute.SAMAccountName;

    [Fact]
    public async Task SearchUsers_SecondIdenticalCall_IsServedFromTheCache()
    {
        var support = new DirectoryTestSupport();
        SetupSearchUsers(support, Entries("jdoe"));

        var handler = new SearchUsersQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var query = SearchQuery("jdoe");

        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        VerifySearchUsers(support, Times.Once());
    }

    [Fact]
    public async Task SearchUsers_DifferentFilterValue_HitsTheDirectoryAgain()
    {
        var support = new DirectoryTestSupport();
        SetupSearchUsers(support, Entries("jdoe"));

        var handler = new SearchUsersQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);

        await handler.Handle(SearchQuery("jdoe"), CancellationToken.None);
        await handler.Handle(SearchQuery("asmith"), CancellationToken.None);

        VerifySearchUsers(support, Times.Exactly(2));
    }

    [Fact]
    public async Task GetUserByIdentifier_MissingEntry_IsNegativelyCached()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.GetUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LdapEntryDto?>.Success(null));

        var handler = new GetUserByIdentifierQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var query = new GetUserByIdentifierQuery(Profile, Catalog, IdAttr, "ghost", Set, UserMustExists: true);

        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.Equal("not_found", first.Error!.Code);
        Assert.False(second.IsSuccess);
        Assert.Equal("not_found", second.Error!.Code);

        support.Connector.Verify(
            c => c.GetUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserByIdentifier_InfrastructureFailure_IsNotCached()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.GetUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LdapEntryDto?>.Failure(Failure));

        var handler = new GetUserByIdentifierQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var query = new GetUserByIdentifierQuery(Profile, Catalog, IdAttr, "jdoe", Set, UserMustExists: true);

        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal("bad_gateway", first.Error!.Code);

        support.Connector.Verify(
            c => c.GetUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<LdapEntryAttributeSet>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SearchUsers_PasswordFilter_IsNotCached()
    {
        var support = new DirectoryTestSupport();
        SetupSearchUsers(support, Entries("jdoe"));

        var handler = new SearchUsersQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var query = new SearchUsersQuery(Profile, Catalog, LdapEntryAttribute.userPassword, "secret", null, null, null, Set);

        await handler.Handle(query, CancellationToken.None);
        await handler.Handle(query, CancellationToken.None);

        VerifySearchUsers(support, Times.Exactly(2));
    }

    [Fact]
    public async Task DeleteMsAdUser_Success_InvalidatesTheProfileTagWithNoCancellation()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.DeleteMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new DeleteMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);

        var result = await handler.Handle(new DeleteMsAdUserCommand(Profile, Catalog, IdAttr, "jdoe"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        support.Invalidator.Verify(
            i => i.InvalidateAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Profile(Profile))),
                It.Is<CancellationToken>(token => token == CancellationToken.None)),
            Times.Once);
    }

    [Fact]
    public async Task DeleteMsAdUser_Failure_DoesNotInvalidate()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.DeleteMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Failure));

        var handler = new DeleteMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);

        var result = await handler.Handle(new DeleteMsAdUserCommand(Profile, Catalog, IdAttr, "jdoe"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        support.VerifyProfileNotInvalidated();
    }

    [Fact]
    public async Task SetMsAdUserPassword_Success_DoesNotInvalidateTheCache_AndKeepsCachedReads()
    {
        var support = new DirectoryTestSupport();
        SetupSearchUsers(support, Entries("jdoe"));
        support.Connector
            .Setup(c => c.SetMsAdUserPasswordAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var searchHandler = new SearchUsersQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var passwordHandler = new SetMsAdUserPasswordCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var query = SearchQuery("jdoe");

        await searchHandler.Handle(query, CancellationToken.None);
        var result = await passwordHandler.Handle(
            new SetMsAdUserPasswordCommand(Profile, Catalog, IdAttr, "jdoe", "N3w-Secret!", false), CancellationToken.None);
        await searchHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        support.VerifyProfileNotInvalidated();
        VerifySearchUsers(support, Times.Once());
        support.VerifyEvent("SetMsAdUserPassword", true);
    }

    [Fact]
    public async Task CreateMsAdUser_Success_InvalidatesTheProfileTag()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.CreateMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<CreateMsAdUserDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LdapEntryDto>.Success(Entry()));

        var handler = new CreateMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var command = new CreateMsAdUserCommand(Profile, Catalog)
        {
            DistinguishedNameOfContainer = "OU=Users,DC=ie,DC=edu,DC=pe",
            DistinguishedName = "CN=John,OU=Users,DC=ie,DC=edu,DC=pe",
            Cn = "John",
            DisplayName = "John Doe",
            SAMAccountName = "jdoe",
            ObjectClass = ["user"],
            Password = "Secret-1!"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        support.VerifyProfileInvalidated();
    }

    [Fact]
    public async Task DisableMsAdUser_Success_InvalidatesTheProfileTag()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.DisableMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new DisableMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);

        var result = await handler.Handle(new DisableMsAdUserCommand(Profile, Catalog, IdAttr, "jdoe", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        support.VerifyProfileInvalidated();
    }

    [Fact]
    public async Task DeleteMsAdUser_InvalidationFailure_DoesNotFailTheCommand()
    {
        var support = new DirectoryTestSupport();
        support.Connector
            .Setup(c => c.DeleteMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        support.Invalidator
            .Setup(i => i.InvalidateAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis is down"));

        var handler = new DeleteMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);

        var result = await handler.Handle(new DeleteMsAdUserCommand(Profile, Catalog, IdAttr, "jdoe"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        support.VerifyEvent("DeleteMsAdUser", true);
    }

    [Fact]
    public async Task SearchUsers_AfterASuccessfulMutation_HitsTheDirectoryAgain()
    {
        var support = new DirectoryTestSupport();
        SetupSearchUsers(support, Entries("jdoe"));
        support.Connector
            .Setup(c => c.DeleteMsAdUserAsync(It.IsAny<LdapRequestContext>(), It.IsAny<LdapIdentifierAttribute>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var searchHandler = new SearchUsersQueryHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var deleteHandler = new DeleteMsAdUserCommandHandler(support.Connector.Object, support.Publisher.Object, support.Cache);
        var query = SearchQuery("jdoe");

        await searchHandler.Handle(query, CancellationToken.None);
        await deleteHandler.Handle(new DeleteMsAdUserCommand(Profile, Catalog, IdAttr, "jdoe"), CancellationToken.None);
        await searchHandler.Handle(query, CancellationToken.None);

        VerifySearchUsers(support, Times.Exactly(2));
    }

    private static SearchUsersQuery SearchQuery(string filterValue) =>
        new(Profile, Catalog, LdapEntryAttribute.cn, filterValue, null, null, null, Set);

    private static void SetupSearchUsers(DirectoryTestSupport support, IReadOnlyList<LdapEntryDto> entries)
    {
        support.Connector
            .Setup(c => c.SearchUsersAsync(
                It.IsAny<LdapRequestContext>(),
                It.IsAny<LdapEntryAttribute>(),
                It.IsAny<string>(),
                It.IsAny<LdapEntryAttribute?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<LdapEntryAttributeSet>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<LdapEntryDto>>.Success(entries));
    }

    private static void VerifySearchUsers(DirectoryTestSupport support, Times times)
    {
        support.Connector.Verify(
            c => c.SearchUsersAsync(
                It.IsAny<LdapRequestContext>(),
                It.IsAny<LdapEntryAttribute>(),
                It.IsAny<string>(),
                It.IsAny<LdapEntryAttribute?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<LdapEntryAttributeSet>(),
                It.IsAny<CancellationToken>()),
            times);
    }
}
