using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

public sealed class ApiRoutesTests : IClassFixture<WebApplicationFactory<Program>>
{
   private readonly HttpClient _client;

   public ApiRoutesTests(WebApplicationFactory<Program> factory)
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
   public async Task GetServerProfileById_ShouldReturnOk_WhenConfiguredProfileExists()
   {
      var response = await _client.GetAsync("/api/ServerProfiles/EDU");

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
   }
}
