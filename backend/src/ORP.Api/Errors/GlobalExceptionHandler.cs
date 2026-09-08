using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ORP.Application.Abstractions;
using ORP.Api.Infrastructure;
using ORP.Domain.Common;

namespace ORP.Api.Errors;

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
            ConcurrentUpdateException => (StatusCodes.Status409Conflict, "Concurrent update"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };
        var userId = ApiLog.UserId(context);
        var correlationId = ApiLog.CorrelationId(context);
        if (status == 500)
            ApiLog.UnexpectedRequestFailed(logger, exception, context.Request.Method, context.Request.Path,
                userId, correlationId);
        else
            ApiLog.ExpectedRequestFailed(logger, exception, context.Request.Method, context.Request.Path,
                status, userId, correlationId);
        context.Response.StatusCode = status;
        var detail = status == 500 ? "An unexpected error occurred." : exception.Message;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail } });
    }
}
