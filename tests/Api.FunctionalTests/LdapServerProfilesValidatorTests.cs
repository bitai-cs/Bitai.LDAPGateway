using Bitai.LDAPGateway.Infrastructure.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Bitai.LDAPGateway.Resource.Api.FunctionalTests;

public sealed class LdapServerProfilesValidatorTests
{
   private const string Secret = "LeakCheck-9f3a!";

   private static LdapServerProfilesValidator CreateValidator(LdapAdapterType adapter) =>
      new(Microsoft.Extensions.Options.Options.Create(new ServiceConnectionAdapterOptions { AdapterType = adapter }));

   private static LdapServerProfilesOptions Profiles(params (string Id, string Password)[] items)
   {
      var options = new LdapServerProfilesOptions();
      options.AddRange(items.Select(i => new LdapServerProfileOption { ProfileId = i.Id, BindAccountPassword = i.Password }));
      return options;
   }

   [Fact]
   public void Novell_WithBlankPassword_Fails_NamingTheProfile()
   {
      var result = CreateValidator(LdapAdapterType.NovellLdapAdapter).Validate(null, Profiles(("EDU", "")));

      Assert.True(result.Failed);
      Assert.Contains("'EDU'", result.FailureMessage);
   }

   [Fact]
   public void Novell_WithPassword_Passes()
   {
      var result = CreateValidator(LdapAdapterType.NovellLdapAdapter).Validate(null, Profiles(("EDU", Secret)));

      Assert.True(result.Succeeded);
   }

   [Fact]
   public void Mock_WithBlankPassword_Passes()
   {
      var result = CreateValidator(LdapAdapterType.MockAdapter).Validate(null, Profiles(("EDU", "")));

      Assert.True(result.Succeeded);
   }

   [Fact]
   public void Novell_WithOneBlankProfile_FailsOnlyForThatProfile_AndLeaksNoPassword()
   {
      var result = CreateValidator(LdapAdapterType.NovellLdapAdapter)
         .Validate(null, Profiles(("EDU", Secret), ("AIR", "  ")));

      Assert.True(result.Failed);
      Assert.Contains("'AIR'", result.FailureMessage);
      Assert.DoesNotContain("'EDU'", result.FailureMessage);
      Assert.DoesNotContain(Secret, result.FailureMessage);
   }

   private sealed class NovellAdapterFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
   {
      private readonly bool _withPasswords;

      public NovellAdapterFactory(bool withPasswords) => _withPasswords = withPasswords;

      protected override void ConfigureWebHost(IWebHostBuilder builder)
      {
         builder.UseEnvironment("Testing");
         builder.ConfigureAppConfiguration((_, config) =>
         {
            var values = new Dictionary<string, string?>
            {
               ["ServiceConnectionAdapter:AdapterType"] = nameof(LdapAdapterType.NovellLdapAdapter)
            };
            if (_withPasswords)
            {
               values["LDAPServerProfiles:0:BindAccountPassword"] = Secret;
               values["LDAPServerProfiles:1:BindAccountPassword"] = Secret;
            }
            config.AddInMemoryCollection(values);
         });
      }
   }

   [Fact]
   public void Host_WithNovellAdapterAndPasswordsFromConfiguration_Starts()
   {
      using var factory = new NovellAdapterFactory(withPasswords: true);

      using var client = factory.CreateClient();

      Assert.NotNull(client);
   }

   [Fact]
   public void Host_WithNovellAdapterAndBlankPasswords_FailsToStart()
   {
      using var factory = new NovellAdapterFactory(withPasswords: false);

      Assert.ThrowsAny<Exception>(() => factory.CreateClient());
   }
}
