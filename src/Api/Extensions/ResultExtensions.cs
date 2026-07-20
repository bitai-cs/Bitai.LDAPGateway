using Bitai.LDAPGateway.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Extensions;

public static class ResultExtensions
{
   public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
   {
      if (result.IsSuccess)
      {
         return controller.Ok(result.Value);
      }

      return controller.Problem(
         statusCode: result.Error?.StatusCode ?? StatusCodes.Status500InternalServerError,
         title: result.Error?.Code ?? "error",
         detail: result.Error?.Message ?? "Unexpected error.");
   }

   public static IActionResult ToActionResult(this ControllerBase controller, Result result)
   {
      if (result.IsSuccess)
      {
         return controller.NoContent();
      }

      return controller.Problem(
         statusCode: result.Error?.StatusCode ?? StatusCodes.Status500InternalServerError,
         title: result.Error?.Code ?? "error",
         detail: result.Error?.Message ?? "Unexpected error.");
   }
}
