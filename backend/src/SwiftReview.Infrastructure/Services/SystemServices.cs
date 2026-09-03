using SwiftReview.Application.Abstractions;

namespace SwiftReview.Infrastructure.Services;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
