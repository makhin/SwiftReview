using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Common;

namespace SwiftReview.Api.Errors;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException or FormatException or BadHttpRequestException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            DomainRuleViolationException => (StatusCodes.Status409Conflict, "Domain rule violation"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };
        if (status == 500) logger.LogError(exception, "Unhandled request exception"); else logger.LogWarning(exception, "Request failed with status {Status}", status);
        context.Response.StatusCode = status;
        var detail = status == 500 ? "An unexpected error occurred." : exception.Message;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail } });
    }
}
