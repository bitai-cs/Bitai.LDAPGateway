using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

[CollectionDefinition(nameof(SwaggerUiOAuthTests), DisableParallelization = true)]
public sealed class SwaggerUiOAuthTestsCollection;

/// <remarks>
/// Settings are passed as environment variables because the API reads configuration while registering services and
/// <c>UseSetting</c> becomes command-line arguments, which JasperFx rejects. The collection is non-parallel so the
/// variables never leak into other test classes.
/// </remarks>
[Collection(nameof(SwaggerUiOAuthTests))]
public sealed class SwaggerUiOAuthTests : IClassFixture<OAuthSecurityWebApplicationFactory>
{
    private const string DocumentPath = "/swagger/Bitai.LDAPWebApi/swagger.json";
    private const string AdminScope = "Bitai.LDAPGateway.Scope.Admin";
    private const string ReaderScope = "Bitai.LDAPGateway.Scope.Reader";
    private const string Authority = "https://test-authority";

    private readonly OAuthSecurityWebApplicationFactory _factory;

    public SwaggerUiOAuthTests(OAuthSecurityWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SecurityActive_DocumentDeclaresOAuth2AuthorizationCodeScheme()
    {
        using var client = CreateClient(Settings(bypass: false));

        using var document = await GetDocumentAsync(client);
        var root = document.RootElement;

        var flow = root.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("oauth2").GetProperty("flows").GetProperty("authorizationCode");
        Assert.Equal($"{Authority}/connect/authorize", flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal($"{Authority}/connect/token", flow.GetProperty("tokenUrl").GetString());

        var scopes = flow.GetProperty("scopes");
        Assert.Equal("Reader title", scopes.GetProperty(ReaderScope).GetString());
        Assert.Equal("Admin title", scopes.GetProperty(AdminScope).GetString());

        Assert.True(root.GetProperty("security")[0].TryGetProperty("oauth2", out _));
    }

    [Fact]
    public async Task BypassEnabled_DocumentHasNoSecurity()
    {
        using var client = CreateClient(new Dictionary<string, string?>
        {
            ["ApiScopesConfiguration:BypassApiScopesAuthorization"] = "true"
        });

        using var document = await GetDocumentAsync(client);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("security", out _));
        Assert.False(root.TryGetProperty("components", out var components) &&
                     components.TryGetProperty("securitySchemes", out _));
    }

    [Theory]
    [InlineData("SwaggerUIApiClient:SwaggerUIClientId", "", "client id is required")]
    [InlineData("SwaggerUIApiClient:SwaggerUIClientSecret", "", "client secret is required")]
    [InlineData("SwaggerUIApiClient:SwaggerUITargetApiScopes:0:SwaggerUITargetApiScopeName", "", "scope with a non-blank name")]
    [InlineData("SwaggerUIApiClient:SwaggerUITargetApiScopes:0:SwaggerUITargetApiScopeName", "Other.Scope", "must match the configured")]
    public void SecurityActive_InvalidSwaggerUiClientConfiguration_FailsStartup(string key, string value, string expectedMessage)
    {
        var settings = Settings(bypass: false);
        settings[key] = value;

        var exception = Record.Exception(() => CreateClient(settings).Dispose());

        Assert.NotNull(exception);
        Assert.Contains(expectedMessage, Flatten(exception), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BypassEnabled_WithoutSwaggerUiClientSection_StartsAndServesDocument()
    {
        using var client = CreateClient(new Dictionary<string, string?>
        {
            ["ApiScopesConfiguration:BypassApiScopesAuthorization"] = "true",
            ["SwaggerUIApiClient:SwaggerUIClientId"] = "",
            ["SwaggerUIApiClient:SwaggerUIClientSecret"] = "",
            ["SwaggerUIApiClient:SwaggerUITargetApiScopes:0:SwaggerUITargetApiScopeName"] = "",
            ["SwaggerUIApiClient:SwaggerUITargetApiScopes:1:SwaggerUITargetApiScopeName"] = ""
        });

        var response = await client.GetAsync(DocumentPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Dictionary<string, string?> Settings(bool bypass) => new()
    {
        ["ApiScopesConfiguration:BypassApiScopesAuthorization"] = bypass ? "true" : "false",
        ["ApiScopesConfiguration:AdminApiScopeName"] = AdminScope,
        ["ApiScopesConfiguration:ReaderApiScopeName"] = ReaderScope,
        ["AuthorityConfiguration:Authority"] = Authority,
        ["AuthorityConfiguration:ApiResource"] = "Bitai.LDAPGateway.Resource.Api",
        ["SwaggerUIApiClient:SwaggerUIClientId"] = "test-swagger-client",
        ["SwaggerUIApiClient:SwaggerUIClientSecret"] = "test-secret",
        ["SwaggerUIApiClient:SwaggerUITargetApiScopes:0:SwaggerUITargetApiScopeName"] = ReaderScope,
        ["SwaggerUIApiClient:SwaggerUITargetApiScopes:0:SwaggerUITargetApiScopeTitle"] = "Reader title",
        ["SwaggerUIApiClient:SwaggerUITargetApiScopes:1:SwaggerUITargetApiScopeName"] = AdminScope,
        ["SwaggerUIApiClient:SwaggerUITargetApiScopes:1:SwaggerUITargetApiScopeTitle"] = "Admin title"
    };

    private HttpClient CreateClient(IDictionary<string, string?> settings)
    {
        var previous = settings.Keys.ToDictionary(key => key, key => Environment.GetEnvironmentVariable(ToVariable(key)));
        try
        {
            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(ToVariable(key), value);
            }

            return _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
        }
        finally
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(ToVariable(key), value);
            }
        }
    }

    private static string ToVariable(string key) => key.Replace(":", "__", StringComparison.Ordinal);

    private static async Task<JsonDocument> GetDocumentAsync(HttpClient client)
    {
        var response = await client.GetAsync(DocumentPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            messages.Add(current.Message);
            if (current.InnerException is null)
            {
                break;
            }
        }

        return string.Join(" | ", messages);
    }
}
