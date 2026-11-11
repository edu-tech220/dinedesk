using DineDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult<T>(ServiceResult<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.Succeeded)
        {
            return onSuccess is not null ? onSuccess(result.Value!) : Ok(result.Value);
        }

        var problem = new ProblemDetails
        {
            Title = result.ErrorType.ToString(),
            Detail = result.Error,
            Status = result.ErrorType switch
            {
                ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
                ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
                ServiceErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest,
            },
        };

        return StatusCode(problem.Status!.Value, problem);
    }
}
