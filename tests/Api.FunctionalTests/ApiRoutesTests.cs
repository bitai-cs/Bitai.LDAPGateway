using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

public sealed class ApiRoutesTests : IClassFixture<OAuthSecurityWebApplicationFactory>
{
   private readonly HttpClient _client;

   public ApiRoutesTests(OAuthSecurityWebApplicationFactory factory)
   {
      _client = factory.CreateClient();
   }

   [Fact]
   public async Task GetCatalogTypes_ShouldReturnOk()
   {
      var response = await _client.GetAsync("/api/CatalogTypes");

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
   }

   [Fact]
   public async Task GetServerProfiles_ShouldReturnOk_WhenConfiguredProfileExists()
   {
      var response = await _client.GetAsync("/api/ServerProfiles");

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
   }
}
