using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

public sealed class OAuthSecurityWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "https://test-authority";
    private const string Audience = "Bitai.LDAPGateway.Resource";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("OAuthSecurityTestsSigningKeyMustBeAtLeastThirtyTwoBytes"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    public WebApplicationFactory<Program> WithStandardSecurity() =>
        WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.ConfigurationManager = null;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = Issuer,
                        ValidateAudience = true,
                        ValidAudience = Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = SigningKey
                    };
                }));
        });

    public static string CreateToken(
        string scope,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null)
    {
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: [new Claim("scope", scope)],
            notBefore: expiry.AddMinutes(-10),
            expires: expiry,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
