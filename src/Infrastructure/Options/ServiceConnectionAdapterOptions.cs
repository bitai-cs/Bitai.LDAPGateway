namespace Bitai.LDAPGateway.Infrastructure.Options;

public sealed class ServiceConnectionAdapterOptions
{
    public const string SectionName = "ServiceConnectionAdapter";
    public string AdapterType { get; set; } = "MockAdapter";
}
