using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ORP.Sync;

internal sealed class SwiftQueryClient
{
    public IReadOnlyList<SwiftMessageData> GetMessages(DateTime fromUtc, DateTime toUtc)
    {
        object query;
        Type queryType;
        if (UseMockQuery())
        {
            query = new MockSwiftQuery();
            queryType = query.GetType();
            Console.WriteLine("Using the temporary SwiftQuery mock.");
        }
        else
        {
            var assemblyName = ConfigurationManager.AppSettings["SwiftAssemblyName"] ?? "Swift";
            var queryTypeName = ConfigurationManager.AppSettings["SwiftQueryTypeName"] ?? "Swift.SwiftQuery";
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch (Exception exception)
            {
                throw new ConfigurationErrorsException(
                    $"Swift assembly '{assemblyName}' is unavailable. Install the approved private NuGet package.", exception);
            }

            queryType = assembly.GetType(queryTypeName, true);
            query = Activator.CreateInstance(queryType);
        }

        Set(query, "AwhApplicationName", "ORP");
        Set(query, "UseSwiftCache", false);
        Set(query, "MessageType", string.Empty);
        Set(query, "MessageDateFrom", DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc));
        Set(query, "MessageDateTo", DateTime.SpecifyKind(toUtc, DateTimeKind.Utc));

        var succeeded = Convert.ToBoolean(queryType.GetMethod("PerformQuery", Type.EmptyTypes).Invoke(query, null),
            CultureInfo.InvariantCulture);
        if (!succeeded)
            throw new InvalidOperationException(Get(query, "Errors")?.ToString() ?? "SwiftQuery failed without details.");

        var messages = Get(query, "Messages") as IEnumerable
            ?? throw new InvalidOperationException("SwiftQuery.Messages is not enumerable.");
        return messages.Cast<object>().Select(Map).ToList();
    }

    private static bool UseMockQuery() =>
        bool.TryParse(ConfigurationManager.AppSettings["UseMockSwiftQuery"], out var enabled) && enabled;

    private static SwiftMessageData Map(object source) => new SwiftMessageData
    {
        WarehouseId = Text(source, "WarehouseId"),
        LoadedDateTime = Date(source, "LoadedDateTime"),
        Json = Text(source, "JSON", "Json"),
        Body = Text(source, "Body"),
        BackendDirection = Text(source, "BackendDirection"),
        CounterParty = Text(source, "CounterParty"),
        CounterPartyCountry = Text(source, "CounterPartyCountry"),
        CreationDate = Date(source, "CreationDate"),
        Direction = Text(source, "Direction"),
        LastModificationDate = Date(source, "LastModificationDate"),
        MessageDate = Date(source, "MessageDate"),
        MessageLength = Number(source, "MessageLength"),
        MessageFormatVersion = Text(source, "MessageFormatVersion"),
        MessageInputReference = Text(source, "MessageInputReference"),
        MessageType = Text(source, "MessageType"),
        MessageTypeShort = Text(source, "MessageTypeShort"),
        ModifiedBy = Text(source, "ModifiedBy"),
        NetworkInterfaceMessageReference = Text(source, "NetworkInterfaceMessageReference"),
        NetworkPriority = Text(source, "NetworkPriority"),
        NetworkProtocol = Text(source, "NetworkProtocol"),
        OriginalStatus = Text(source, "OriginalStatus"),
        OwnBic = Text(source, "OwnBic"),
        PossibleDuplicate = Flag(source, "PossibleDuplicate"),
        ReceiverResponder = Text(source, "ReceiverResponder"),
        ReceiverResponderBic8 = Text(source, "ReceiverResponderBIC8", "ReceiverResponderBic8"),
        SenderRequestor = Text(source, "SenderRequestor"),
        SenderRequestorBic8 = Text(source, "SenderRequestorBIC8", "SenderRequestorBic8"),
        SequenceNumber = Text(source, "SequenceNumber"),
        SessionNumber = Text(source, "SessionNumber"),
        Service = Text(source, "Service"),
        SourceInterface = Text(source, "SourceInterface"),
        Status = Text(source, "Status"),
        StatusDate = Date(source, "StatusDate"),
        TouchedByHuman = Flag(source, "TouchedByHuman"),
        Uetr = Text(source, "UETR", "Uetr"),
        Accounts = Strings(source, "Accounts"),
        Amounts = Decimals(source, "Amounts"),
        Currencies = Strings(source, "Currency", "Currencies"),
        BeneficiaryCustomerAccounts = Strings(source, "BeneficiaryCustomerAccount"),
        BeneficiaryCustomerBanks = Strings(source, "BeneficiaryCustomerBank"),
        BeneficiaryCustomerNames = Strings(source, "BeneficiaryCustomerName"),
        OrderingCustomerAccounts = Strings(source, "OrderingCustomerAccount"),
        OrderingCustomerBanks = Strings(source, "OrderingCustomerBank"),
        OrderingCustomerNames = Strings(source, "OrderingCustomerName"),
        SenderMessageReferences = Strings(source, "SenderMessageReference"),
        SettlementDates = Dates(source, "SettlementDates"),
        TradeDealDates = Dates(source, "TradeDealDate", "TradeDealDates"),
        UnitDataOwners = Strings(source, "UnitDataOwner"),
        ValueDates = Dates(source, "ValueDate", "ValueDates")
    };

    private static object Get(object source, params string[] names)
    {
        foreach (var name in names)
        {
            var property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null) return property.GetValue(source, null);
        }
        return null;
    }

    private static void Set(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(target.GetType().FullName, name);
        property.SetValue(target, value, null);
    }

    private static string Text(object source, params string[] names) => Get(source, names)?.ToString();
    private static bool Flag(object source, params string[] names) => Convert.ToBoolean(Get(source, names) ?? false, CultureInfo.InvariantCulture);
    private static int Number(object source, params string[] names) => Convert.ToInt32(Get(source, names) ?? 0, CultureInfo.InvariantCulture);

    private static DateTime? Date(object source, params string[] names)
    {
        var value = Get(source, names);
        if (value == null) return null;
        if (value is DateTime date) return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        if (value is DateTimeOffset offset) return offset.UtcDateTime;
        return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : (DateTime?)null;
    }

    private static IReadOnlyList<object> Items(object source, params string[] names)
    {
        var values = Get(source, names) as IEnumerable;
        return values == null ? Array.Empty<object>() : values.Cast<object>().ToList();
    }

    private static IReadOnlyList<string> Strings(object source, params string[] names) =>
        Items(source, names).Select(value => value?.ToString()).ToList();

    private static IReadOnlyList<DateTime?> Dates(object source, params string[] names) =>
        Items(source, names).Select(value => value == null ? null : DateFromValue(value)).ToList();

    private static DateTime? DateFromValue(object value)
    {
        if (value is DateTime date) return date.Date;
        if (value is DateTimeOffset offset) return offset.UtcDateTime.Date;
        return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date : (DateTime?)null;
    }

    private static IReadOnlyList<decimal?> Decimals(object source, params string[] names) =>
        Items(source, names).Select(DecimalFromValue).ToList();

    private static decimal? DecimalFromValue(object value)
    {
        if (value == null) return null;
        if (value is decimal number) return number;
        foreach (var propertyName in new[] { "Amount", "Value" })
        {
            var nested = value.GetType().GetProperty(propertyName)?.GetValue(value, null);
            if (nested != null && decimal.TryParse(nested.ToString(), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out number)) return number;
        }
        var text = value.ToString();
        var firstDigit = text.TakeWhile(character => !char.IsDigit(character) && character != '-').Count();
        return decimal.TryParse(text.Substring(firstDigit), NumberStyles.Number, CultureInfo.InvariantCulture,
            out number) ? number : (decimal?)null;
    }
}
