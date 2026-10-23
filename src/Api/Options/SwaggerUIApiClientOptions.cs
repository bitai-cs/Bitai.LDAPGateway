namespace Bitai.LDAPGateway.Api.Options;

/// <summary>
/// Settings the Swagger UI uses to authenticate against the configured OAuth/OIDC authority.
/// </summary>
public sealed class SwaggerUIApiClientOptions
{
    public const string SectionName = "SwaggerUIApiClient";

    public string SwaggerUIClientId { get; set; } = string.Empty;
    public string SwaggerUIClientSecret { get; set; } = string.Empty;
    public List<SwaggerUITargetApiScope> SwaggerUITargetApiScopes { get; set; } = [];
}

/// <summary>
/// An API scope the Swagger UI can request, with the title shown in the authorization dialog.
/// </summary>
public sealed class SwaggerUITargetApiScope
{
    public string SwaggerUITargetApiScopeName { get; set; } = string.Empty;
    public string SwaggerUITargetApiScopeTitle { get; set; } = string.Empty;
}
