namespace Bitai.LDAPGateway.Api.Options;

public sealed class WebApiConfigurationOptions
{
    public const string SectionName = "WebApiConfiguration";
    public string WebApiName { get; set; } = "Bitai.LDAPWebApi";
    public string WebApiTitle { get; set; } = string.Empty;
    public string WebApiDescription { get; set; } = string.Empty;
    public string WebApiVersion { get; set; } = "v1";
    public string WebApiContactName { get; set; } = string.Empty;
    public string WebApiContactMail { get; set; } = string.Empty;
    public string WebApiContactUrl { get; set; } = string.Empty;
    public string WebApiLicenseName { get; set; } = "MIT";
    public bool SwaggerUI { get; set; } = true;
}
