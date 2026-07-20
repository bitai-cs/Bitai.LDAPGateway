namespace Bitai.LDAPGateway.Api.Options;

public sealed class WebApiCorsConfiguration
{
   public const string SectionName = "WebApiCorsConfiguration";

   public bool AllowAnyOrigin { get; set; }

   public List<string> AllowedOrigins { get; set; } = [];
}
