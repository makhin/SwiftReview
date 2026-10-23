using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Api.Errors;
using ORP.Application.Abstractions;
using ORP.Application.Messages.Search;
using ORP.Domain.Common;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class ApiErrorTests
{
    public static TheoryData<Exception, int, string> Errors => new()
    {
        { new ResourceNotFoundException("Missing"), 404, "not_found" },
        { new UnauthorizedAccessException("Private reason"), 403, "forbidden" },
        { new DomainRuleViolationException("Conflict"), 409, "domain_conflict" },
        { new ConcurrentUpdateException("Changed", new Exception()), 409, "concurrent_update" },
        { new TransactionCommitOutcomeUnknownException(new IOException("Private connection")), 500, "commit_outcome_unknown" },
        { new InvalidOperationException("Private SQL"), 500, "unexpected_error" },
        { new FormatException("Bad format"), 400, "validation_failed" },
        { new BadHttpRequestException("Too large", 413), 413, "validation_failed" }
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task ErrorsHaveStableCodesAndDoNotExposeInternalFailures(Exception exception, int status, string code)
    {
        var writer = new CaptureProblems();
        var handler = new GlobalExceptionHandler(writer, NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(writer.Problem);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
        Assert.DoesNotContain("Private", problem.Detail!);
        if (code == "commit_outcome_unknown") Assert.Contains("Reload the message before retrying", problem.Detail);
    }

    [Fact]
    public async Task ValidationErrorsUseJsonFieldNamesAndRetainAllDistinctMessages()
    {
        var writer = new CaptureProblems();
        var handler = new GlobalExceptionHandler(writer, NullLogger<GlobalExceptionHandler>.Instance);
        var exception = new ValidationException([
            new ValidationFailure("MessageIds[0]", "Invalid ID"),
            new ValidationFailure("Comment", "Too long"),
            new ValidationFailure("Comment", "Too long"),
            new ValidationFailure("Comment", "Invalid text")]);
        await handler.TryHandleAsync(new DefaultHttpContext(), exception, CancellationToken.None);
        var errors = Assert.IsType<Dictionary<string, string[]>>(writer.Problem!.Extensions["errors"]);
        Assert.Equal(new[] { "Invalid ID" }, errors["messageIds[0]"]);
        Assert.Equal(new[] { "Too long", "Invalid text" }, errors["comment"]);
    }

    [Fact]
    public async Task NestedSortValidationErrorsUseJsonPaths()
    {
        var request = new MessageSearchRequest(0, 10, [new SortClause("unknown", "invalid")], null);
        var validation = await new MessageSearchValidator().ValidateAsync(request, TestContext.Current.CancellationToken);
        var writer = new CaptureProblems();
        var handler = new GlobalExceptionHandler(writer, NullLogger<GlobalExceptionHandler>.Instance);
        await handler.TryHandleAsync(new DefaultHttpContext(), new ValidationException(validation.Errors),
            TestContext.Current.CancellationToken);
        var errors = Assert.IsType<Dictionary<string, string[]>>(writer.Problem!.Extensions["errors"]);
        Assert.Equal(new[] { "sort[0].direction", "sort[0].field" }, errors.Keys.Order());
        Assert.Equal(new[] { "Unsupported sort field." }, errors["sort[0].field"]);
        Assert.NotEmpty(errors["sort[0].direction"]);
    }

    private sealed class CaptureProblems : IProblemDetailsService
    {
        public ProblemDetails? Problem { get; private set; }
        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }
        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
