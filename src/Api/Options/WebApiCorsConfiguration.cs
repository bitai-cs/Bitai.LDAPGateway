namespace Bitai.LDAPGateway.Api.Options;

/// <summary>Configuration options: web api cors configuration.</summary>
public sealed class WebApiCorsConfiguration
{
    public const string SectionName = "WebApiCorsConfiguration";

    public string CorsPolicyName {get; set;} = "LdapGatewayCorsPolicy";

    public bool AllowAnyOrigin { get; set; }

    public List<string> AllowedOrigins { get; set; } = [];
}
