namespace SwiftReview.Domain.Common;

public sealed class DomainRuleViolationException(string message) : Exception(message);
