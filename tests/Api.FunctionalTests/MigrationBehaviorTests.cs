using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bitai.LDAPGateway.Api.FunctionalTests;

/// <summary>Regression tests proving the ported MediatR behaviors still run under Wolverine.</summary>
public sealed class MigrationBehaviorTests : IClassFixture<WebApplicationFactory<Program>>
{
   private const string Secret = "LeakCheck-9f3a!";

   private readonly WebApplicationFactory<Program> _factory;

   public MigrationBehaviorTests(WebApplicationFactory<Program> factory) => _factory = factory;

   private (HttpClient Client, CapturedLogs Logs) CreateClient()
   {
      var logs = new CapturedLogs();
      var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddLogging(l => l.AddProvider(logs)))).CreateClient();
      return (client, logs);
   }

   private (IServiceScope Scope, CapturedLogs Logs) CreateScope()
   {
      var logs = new CapturedLogs();
      var scope = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddLogging(l => l.AddProvider(logs)))).Services.CreateScope();
      return (scope, logs);
   }

   private static bool Leaks((string Category, LogLevel Level, string Message, Exception? Exception, string Scopes) e)
      => e.Message.Contains(Secret) || (e.Exception?.ToString().Contains(Secret) ?? false);

   [Fact]
   public async Task Success_IsLogged()
   {
      var (client, logs) = CreateClient();

      var response = await client.GetAsync("/api/CatalogTypes");

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      Assert.Contains(logs.Entries, e => e.Message == "Handling request." && e.Level == LogLevel.Information);
      Assert.Contains(logs.Entries, e => e.Message == "Request completed successfully.");
   }

   [Fact]
   public async Task FailureResult_IsLoggedAsWarning_WithRequestNameScope()
   {
      var (client, logs) = CreateClient();

      var response = await client.GetAsync("/api/ServerProfiles/NOPE_PROFILE");

      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Request failed [not_found] (404)"));
      Assert.Contains(logs.Entries, e => e.Message == "Handling request." && e.Scopes.Contains("RequestName=GetProfileByIdQuery"));
   }

   [Fact]
   public async Task InvalidCommand_IsRejectedWith400AndHandlerDoesNotRun()
   {
      var (client, logs) = CreateClient();

      // Profile ids come from configuration (appsettings.Development.json differs per machine): discover one.
      var profile = (await client.GetFromJsonAsync<string[]>("/api/ServerProfiles/GetProfileIds"))![0];

      while (logs.Entries.TryDequeue(out _)) { }   // drop the discovery request's log entries

      var response = await client.PostAsJsonAsync($"/api/{profile}/LC/Authentications/authenticate", new { Username = "", Password = "x" });

      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Contains("Validation failed", await response.Content.ReadAsStringAsync());
      Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("Request completed successfully."));
   }

   [Fact]
   public async Task FailedSetPasswordCommand_DoesNotWritePasswordToLogs()
   {
      var (scope, logs) = CreateScope();
      using (scope)
      {
         var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();

         // Blank identifier fails validation while NewPassword is populated.
         var command = new Application.Directory.Commands.SetMsAdUserPassword.SetMsAdUserPasswordCommand(
            "EDU", Domain.Enums.CatalogType.LC, default, " ", Secret, false);

         await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => bus.InvokeAsync<Application.Common.Models.Result>(command));

         Assert.DoesNotContain(logs.Entries, Leaks);
      }
   }

   [Fact]
   public async Task FailedCreateUserCommand_DoesNotWritePasswordToLogs()
   {
      var (scope, logs) = CreateScope();
      using (scope)
      {
         var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();

         // CatalogType.LC is rejected by the validator (cannot create user accounts in the global catalog).
         var command = new Application.Directory.Commands.CreateMsAdUser.CreateMsAdUserCommand("EDU", Domain.Enums.CatalogType.LC)
         {
            Password = Secret
         };

         await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => bus.InvokeAsync<Application.Common.Models.Result<Application.Common.Models.LdapEntryDto>>(command));

         Assert.DoesNotContain(logs.Entries, Leaks);
      }
   }

   [Fact]
   public async Task DomainEvent_IsDeliveredToNotificationHandlerInline()
   {
      var (scope, logs) = CreateScope();
      using (scope)
      {
         var publisher = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IDomainEventPublisher>();

         await publisher.PublishAsync(
            new Domain.Events.OperationCompletedDomainEvent("TestOp", "EDU", Domain.Enums.CatalogType.LC, true, "ok", DateTime.UtcNow),
            CancellationToken.None);

         Assert.Contains(logs.Entries, e => e.Message.Contains("LDAP operation TestOp completed"));
      }
   }
}
