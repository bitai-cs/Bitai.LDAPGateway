using System.Net;
using System.Net.Http.Headers;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

public sealed class OAuthSecurityTests : IClassFixture<OAuthSecurityWebApplicationFactory>
{
    private readonly OAuthSecurityWebApplicationFactory _factory;

    public OAuthSecurityTests(OAuthSecurityWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task StandardMode_RejectsMissingCredential()
    {
        using var client = _factory.WithStandardSecurity().CreateClient();

        var response = await client.GetAsync("/api/CatalogTypes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StandardMode_RejectsExpiredCredential()
    {
        using var client = _factory.WithStandardSecurity().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            OAuthSecurityWebApplicationFactory.CreateToken(
                "Bitai.LDAPGateway.Scope.Admin",
                DateTime.UtcNow.AddMinutes(-10)));

        var response = await client.GetAsync("/api/CatalogTypes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("https://wrong-authority", "Bitai.LDAPGateway.Resource")]
    [InlineData("https://test-authority", "wrong-resource")]
    public async Task StandardMode_RejectsWrongIssuerOrAudience(string issuer, string audience)
    {
        using var client = _factory.WithStandardSecurity().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            OAuthSecurityWebApplicationFactory.CreateToken(
                "Bitai.LDAPGateway.Scope.Admin",
                issuer: issuer,
                audience: audience));

        var response = await client.GetAsync("/api/CatalogTypes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Bitai.LDAPGateway.Scope.Admin")]
    [InlineData("Bitai.LDAPGateway.Scope.Reader")]
    public async Task StandardMode_AllowsConfiguredScopeForReaderEndpoint(string scope)
    {
        using var client = _factory.WithStandardSecurity().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            OAuthSecurityWebApplicationFactory.CreateToken(scope));

        var response = await client.GetAsync("/api/CatalogTypes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StandardMode_RejectsReaderScopeForAdministratorOnlyEndpoint()
    {
        using var client = _factory.WithStandardSecurity().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            OAuthSecurityWebApplicationFactory.CreateToken("Bitai.LDAPGateway.Scope.Reader"));

        var response = await client.DeleteAsync("/api/EDU/LC/Directory/MsADUsers/example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
