using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class LoggingTests
{
    [Fact]
    public async Task InvalidAssignment_PreservesSuppliedCorrelationIdInFailureAndCompletionLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.UseSetting("UseMockData", "true");
            web.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "invalid-assignment-correlation");

        using var response = await client.PostAsJsonAsync("/api/messages/1/assign",
            new AssignMessageRequest(0), ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var failure = Assert.Single(logs.Entries, entry => entry.EventId.Id == 1020);
        var completion = Assert.Single(logs.Entries, entry => entry.EventId.Id == 1000);
        Assert.Equal(400, completion.Property<int>("StatusCode"));
        Assert.Equal("invalid-assignment-correlation", failure.Property<string>("CorrelationId"));
        Assert.Equal(failure.Property<string>("CorrelationId"), completion.Property<string>("CorrelationId"));
    }

    [Fact]
    public async Task RequestsAndCommittedBusinessActions_AreLoggedWithContext()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.UseSetting("UseMockData", "true");
            web.UseSetting("ConnectionStrings:ORP", "not-a-sql-server-connection");
            web.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        using var client = factory.CreateClient();

        using (var unauthorized = await client.GetAsync("/api/me", ct))
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "logging-test");
        using (var assign = await client.PostAsJsonAsync("/api/messages/1/assign",
                   new AssignMessageRequest(1), ct))
            Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var denied = Assert.Single(logs.Entries, entry => entry.EventId.Id == 1010);
        Assert.Equal(LogLevel.Warning, denied.Level);
        Assert.Equal(401, denied.Property<int>("StatusCode"));

        var request = Assert.Single(logs.Entries, entry => entry.EventId.Id == 1000 &&
            entry.Property<string>("RequestPath") == "/api/messages/1/assign");
        Assert.Equal(LogLevel.Information, request.Level);
        Assert.Equal(204, request.Property<int>("StatusCode"));
        Assert.Equal(5, request.Property<int?>("UserId"));
        Assert.Equal("logging-test", request.Property<string>("CorrelationId"));
        Assert.True(request.Property<double>("ElapsedMilliseconds") >= 0);

        var action = Assert.Single(logs.Entries, entry => entry.EventId.Id == 2000 &&
            Equals(entry.Property<object>("EventType"), AuditEventType.MessageAssigned));
        Assert.Equal(1L, action.Property<long>("MessageId"));
        Assert.Equal(5, action.Property<int?>("ActorUserId"));
        Assert.Equal("logging-test", action.Property<string>("CorrelationId"));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            entries.Enqueue(new LogEntry(category, logLevel, eventId, properties));
        }
    }

    private sealed record LogEntry(string Category, LogLevel Level, EventId EventId,
        IReadOnlyList<KeyValuePair<string, object?>> Properties)
    {
        public T Property<T>(string name) =>
            (T)Properties.Single(property => property.Key == name).Value!;
    }
}
