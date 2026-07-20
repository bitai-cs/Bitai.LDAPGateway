namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record Error(string Code, string Message, int StatusCode)
{
   public static Error Validation(string message) => new("validation_error", message, 400);
   public static Error NotFound(string message) => new("not_found", message, 404);
   public static Error Conflict(string message) => new("conflict", message, 409);
   public static Error Internal(string message) => new("internal_error", message, 500);
   public static Error BadGateway(string message) => new("bad_gateway", message, 502);
}
