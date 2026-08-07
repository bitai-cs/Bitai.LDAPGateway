using System.Diagnostics;

namespace Bitai.LDAPGateway.Domain.ValueObjects;

public sealed record UserCredential(string Username, Secret Password);

[DebuggerDisplay("{ToString()}")]
public readonly record struct Secret
{
    private readonly string _value;



    public Secret(string value) => _value = value;



    public string DangerousGetSecret() => _value;

    public override string ToString() => "***";
}
