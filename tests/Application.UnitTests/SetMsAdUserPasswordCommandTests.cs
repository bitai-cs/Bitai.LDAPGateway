using Bitai.LDAPGateway.Application.Directory.Commands.SetMsAdUserPassword;
using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class SetMsAdUserPasswordCommandTests
{
   private const string Secret = "LeakCheck-9f3a!";

   [Fact]
   public void ToString_MasksNewPassword()
   {
      var command = new SetMsAdUserPasswordCommand("EDU", CatalogType.GC, default, "jdoe", Secret, false);

      var text = command.ToString();

      Assert.DoesNotContain(Secret, text);
      Assert.Contains("NewPassword = ***", text);
   }
}
