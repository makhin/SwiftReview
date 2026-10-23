using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Domain.Common;

namespace ORP.Application.Errors;

public sealed record OperationError(int Status, string Code, string Title, string Detail)
{
    public static OperationError From(Exception exception) => exception switch
    {
        ValidationException or FormatException => new(400, "validation_failed", "Validation failed", exception.Message),
        ResourceNotFoundException => new(404, "not_found", "Resource not found", exception.Message),
        UnauthorizedAccessException => new(403, "forbidden", "Forbidden", "The current user is not allowed to perform this action."),
        DomainRuleViolationException => new(409, "domain_conflict", "Domain rule violation", exception.Message),
        ConcurrentUpdateException => new(409, "concurrent_update", "Concurrent update", exception.Message),
        TransactionCommitOutcomeUnknownException => new(500, "commit_outcome_unknown", "Commit outcome unknown",
            "The commit outcome is unknown. Reload the message before retrying."),
        _ => new(500, "unexpected_error", "Unexpected error", "An unexpected error occurred.")
    };
}
