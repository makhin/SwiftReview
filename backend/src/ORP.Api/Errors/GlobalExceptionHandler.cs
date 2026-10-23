using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ORP.Application.Errors;
using ORP.Api.Infrastructure;

namespace ORP.Api.Errors;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var error = exception is BadHttpRequestException badRequest
            ? new OperationError(badRequest.StatusCode, "validation_failed", "Validation failed", badRequest.Message)
            : OperationError.From(exception);
        var status = error.Status;
        var userId = ApiLog.UserId(context);
        var correlationId = ApiLog.CorrelationId(context);
        if (status == 500)
            ApiLog.UnexpectedRequestFailed(logger, exception, context.Request.Method, context.Request.Path,
                userId, correlationId);
        else
            ApiLog.ExpectedRequestFailed(logger, exception, context.Request.Method, context.Request.Path,
                status, userId, correlationId);
        context.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = error.Title, Detail = error.Detail };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = correlationId;
        if (exception is ValidationException validation)
            problem.Extensions["errors"] = validation.Errors.GroupBy(failure => ToJsonPath(failure.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }

    private static string ToJsonPath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

}
