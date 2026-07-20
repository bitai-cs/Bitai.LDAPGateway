using FluentValidation;

namespace Bitai.LDAPGateway.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
   private readonly RequestDelegate _next;
   private readonly ILogger<ExceptionHandlingMiddleware> _logger;

   public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
   {
      _next = next;
      _logger = logger;
   }

   public async Task Invoke(HttpContext context)
   {
      try
      {
         await _next(context);
      }
      catch (ValidationException ex)
      {
         context.Response.StatusCode = StatusCodes.Status400BadRequest;
         await context.Response.WriteAsJsonAsync(new
         {
            type = "https://datatracker.ietf.org/doc/html/rfc7807",
            title = "Validation failed",
            status = StatusCodes.Status400BadRequest,
            detail = ex.Message,
            errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
         });
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Unhandled exception in request pipeline");

         context.Response.StatusCode = StatusCodes.Status500InternalServerError;
         await context.Response.WriteAsJsonAsync(new
         {
            type = "https://datatracker.ietf.org/doc/html/rfc7807",
            title = "Unexpected server error",
            status = StatusCodes.Status500InternalServerError,
            detail = "An unexpected error occurred."
         });
      }
   }
}
