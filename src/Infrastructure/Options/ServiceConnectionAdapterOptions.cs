namespace Bitai.LDAPGateway.Infrastructure.Options;

public enum LdapAdapterType
{
    NovellLdapAdapter,
    MockAdapter
}

public sealed class ServiceConnectionAdapterOptions
{
    public const string SectionName = "ServiceConnectionAdapter";
    public LdapAdapterType AdapterType { get; set; } = LdapAdapterType.MockAdapter;
}
