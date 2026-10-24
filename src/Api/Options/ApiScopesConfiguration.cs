namespace Bitai.LDAPGateway.Api.Options;

/// <summary>Configuration options: api scopes configuration.</summary>
public sealed class ApiScopesConfiguration
{
    public const string SectionName = "ApiScopesConfiguration";

    public bool BypassApiScopesAuthorization { get; set; }
    public string AdminApiScopeName { get; set; } = string.Empty;
    public string ReaderApiScopeName { get; set; } = string.Empty;
}
