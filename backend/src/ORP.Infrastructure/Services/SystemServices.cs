using ORP.Application.Abstractions;

namespace ORP.Infrastructure.Services;

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
