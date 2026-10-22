namespace Bitai.LDAPGateway.Api.Options;

public sealed class AuthorityConfiguration
{
    public const string SectionName = "AuthorityConfiguration";

    public string Authority { get; set; } = string.Empty;
    public string ApiResource { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
}
