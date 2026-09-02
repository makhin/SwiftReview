using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace SwiftReview.Api.Authorization;

public sealed class ProblemDetailsAuthorizationResultHandler(IProblemDetailsService problemDetails)
    : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var status = authorizeResult.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        context.Response.StatusCode = status;
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status == StatusCodes.Status403Forbidden ? "Forbidden" : "Unauthorized",
                Detail = status == StatusCodes.Status403Forbidden
                    ? "The current user is not allowed to perform this action."
                    : "Authentication is required."
            }
        });
    }
}
