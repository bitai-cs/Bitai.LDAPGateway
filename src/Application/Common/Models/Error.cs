namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record Error(string Code, string Message, int StatusCode, Error? InnerError = null)
{
    // Attach an innerError error to an existing Error (non-mutating)
    public Error WithInner(Error innerError) => this with { InnerError = innerError };

    // Walk the whole chain, this error first
    public IEnumerable<Error> Flatten()
    {
        var current = this;
        while (current is not null)
        {
            yield return current;
            current = current.InnerError;
        }
    }



    //// Old version of the factory methods
    //public static Error Validation(string message) => new("validation_error", message, 400);
    //public static Error NotFound(string message) => new("not_found", message, 404);
    //public static Error Conflict(string message) => new("conflict", message, 409);
    //public static Error Internal(string message) => new("internal_error", message, 500);
    //public static Error BadGateway(string message) => new("bad_gateway", message, 502);
    // New version of the factory methods with optional innerError parameter
    public static Error Validation(string message, Error? innerError = null) => new("validation_error", message, 400, innerError);
    public static Error NotFound(string message, Error? innerError = null) => new("not_found", message, 404, innerError);
    public static Error Conflict(string message, Error? innerError = null) => new("conflict", message, 409, innerError);
    public static Error Internal(string message, Error? innerError = null) => new("internal_error", message, 500, innerError);
    public static Error BadGateway(string message, Error? innerError = null) => new("bad_gateway", message, 502, innerError);
    public static Error InnerErr(string message, Error? innerError = null) => new("inner_error", message, 0, innerError);
}
