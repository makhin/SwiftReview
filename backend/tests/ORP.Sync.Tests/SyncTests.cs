using System.Configuration;
using Microsoft.Extensions.Logging;
using ORP.Sync;
using Xunit;

namespace ORP.Sync.Tests;

// These tests temporarily change process-wide ConfigurationManager settings.
[CollectionDefinition("Sync configuration", DisableParallelization = true)]
public sealed class SyncConfigurationCollection;

[Collection("Sync configuration")]
public sealed class SyncTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MockQuery_MapsPayloadCollectionsAndStableWarehouseId()
    {
        using var config = new Settings("UseMockSwiftQuery", "true");
        var logger = new CapturingLogger<SwiftQueryClient>();
        var client = new SwiftQueryClient(logger);
        var message = Assert.Single(client.GetMessages(Now.AddHours(-1), Now));
        Assert.Equal("MT103", message.MessageType);
        Assert.Equal("MOCK-SWIFT-MT103-001", message.WarehouseId);
        Assert.Equal(Now.AddMinutes(-1), message.MessageDate);
        Assert.Equal(DateTimeKind.Utc, message.MessageDate.Value.Kind);
        Assert.Contains("\"source\":\"mock\"", message.Json);
        Assert.Equal(message.Body.Length, message.MessageLength);
        Assert.Equal("AAAAUS33", message.ReceiverResponderBic8);
        Assert.Equal("11111111-2222-4333-8444-555555555555", message.Uetr);
        Assert.Equal(new[] { "USD", "GBP" }, message.Currencies);
        Assert.Equal(new decimal?[] { 1250.50m, 25m }, message.Amounts);
        Assert.Equal(new[] { "Mock Beneficiary One", "Mock Beneficiary Two" }, message.BeneficiaryCustomerNames);
        Assert.Equal(2, message.EntryCount);
        Assert.All(message.CollectionLengths, count => Assert.Equal(2, count));
        Assert.Equal(message.WarehouseId,
            Assert.Single(client.GetMessages(Now, Now.AddHours(1))).WarehouseId);
        Assert.Contains(logger.Events, entry => entry.EventId.Id == 3002 && entry.Level == LogLevel.Information);
    }

    [Fact]
    public void RealQuery_MissingAssemblyFailsInsteadOfReturningMock()
    {
        using var mock = new Settings("UseMockSwiftQuery", "false");
        using var assembly = new Settings("SwiftAssemblyName", "ORP.Nonexistent.Swift.TestAssembly");
        Assert.Throws<ConfigurationErrorsException>(() => new SwiftQueryClient().GetMessages(Now, Now));
    }

    [Theory]
    [InlineData("OwnBic", "aaaaus33", "AAAAUS33XXX")]
    [InlineData("OwnBic", "AAAA*", "AAAAUS33XXX")]
    [InlineData("SenderRequestor", "AAAAUS33", "AAAAUS33XXX")]
    [InlineData("ReceiverResponder", "AAAAUS33", "AAAAUS33XXX")]
    public void Routing_MatchesBicDirectionAndShortMessageType(string field, string pattern, string bic)
    {
        using var config = new Settings("RoutingRules", $"10|{field}|{pattern}|in|103|1|2");
        var message = new SwiftMessageData { OwnBic = bic, SenderRequestor = bic,
            ReceiverResponder = bic, Direction = " IN ", MessageType = "MT103", MessageTypeShort = "103" };
        var route = RoutingRules.FromConfiguration().Resolve(message);
        Assert.True(route.IsRouted);
        Assert.Equal(1, route.BranchId);
        Assert.Equal(2, route.DepartmentId);
        message.Direction = "OUT";
        Assert.False(RoutingRules.FromConfiguration().Resolve(message).IsRouted);
        message.Direction = "IN";
        message.MessageTypeShort = "199";
        Assert.False(RoutingRules.FromConfiguration().Resolve(message).IsRouted);
    }

    [Fact]
    public void Routing_UsesLowestPriorityAndReportsDistinctReferences()
    {
        using var config = new Settings("RoutingRules", "20|OwnBic||||2|3;10|OwnBic||||1|3");
        var rules = RoutingRules.FromConfiguration();
        Assert.Equal(1, rules.Resolve(new SwiftMessageData()).BranchId);
        Assert.Equal(new[] { 1, 2 }, rules.BranchIds);
        Assert.Equal(new[] { 3 }, rules.DepartmentIds);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("10|Unknown||||1|2")]
    [InlineData("10|OwnBic||||1|2;10|OwnBic||||2|3")]
    public void Routing_RejectsInvalidConfiguration(string setting)
    {
        using var config = new Settings("RoutingRules", setting);
        Assert.Throws<ConfigurationErrorsException>(() => RoutingRules.FromConfiguration());
    }

    [Fact]
    public void EmptyRules_LeaveMessageUnroutable_AndEntryCountUsesLongestCollection()
    {
        using var config = new Settings("RoutingRules", "");
        var message = new SwiftMessageData { Accounts = new[] { "one" }, Amounts = new decimal?[] { 1, 2, 3 } };
        Assert.Equal(3, message.EntryCount);
        var route = RoutingRules.FromConfiguration().Resolve(message);
        Assert.False(route.IsRouted);
        Assert.NotEmpty(route.Error);
        Assert.Equal(0, new SwiftMessageData().EntryCount);
    }

    private sealed class Settings : IDisposable
    {
        private readonly string _key;
        private readonly string _previous;
        public Settings(string key, string value)
        {
            _key = key;
            _previous = ConfigurationManager.AppSettings[key];
            ConfigurationManager.AppSettings[key] = value;
        }
        public void Dispose()
        {
            ConfigurationManager.AppSettings[_key] = _previous;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, EventId EventId)> Events { get; } = new();
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter) => Events.Add((logLevel, eventId));
    }
}
