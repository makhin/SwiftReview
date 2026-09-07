using System;
using System.Collections.Generic;

namespace ORP.Sync;

// Temporary stand-in for the private Swift.SwiftQuery package. It intentionally exposes the same
// public surface used by SwiftQueryClient so that the reflection mapping path is exercised locally.
internal sealed class MockSwiftQuery
{
    public string AwhApplicationName { get; set; }
    public bool UseSwiftCache { get; set; }
    public string MessageType { get; set; }
    public DateTime MessageDateFrom { get; set; }
    public DateTime MessageDateTo { get; set; }
    public string Errors { get; private set; }
    public IReadOnlyList<MockSwiftMessage> Messages { get; private set; } = Array.Empty<MockSwiftMessage>();

    public bool PerformQuery()
    {
        var messageDate = MessageDateTo == default(DateTime) ? DateTime.UtcNow : MessageDateTo.AddMinutes(-1);
        const string body = "{1:F01AAAAUS33AXXX0000000000}{2:I103BBBBGB22XXXXN}{4:\n:20:MOCK-REFERENCE\n:32A:260907USD1250,50\n-}";
        Messages = new[]
        {
            new MockSwiftMessage
            {
                WarehouseId = "MOCK-SWIFT-MT103-001",
                LoadedDateTime = messageDate,
                JSON = "{\"source\":\"mock\",\"messageType\":\"MT103\"}",
                Body = body,
                BackendDirection = "Inbound",
                CounterParty = "BBBBGB22XXX",
                CounterPartyCountry = "GB",
                CreationDate = messageDate,
                Direction = "IN",
                LastModificationDate = messageDate,
                MessageDate = messageDate,
                MessageLength = body.Length,
                MessageFormatVersion = "FIN",
                MessageInputReference = "MOCK-INPUT-REFERENCE",
                MessageType = "MT103",
                MessageTypeShort = "103",
                ModifiedBy = "MOCK",
                NetworkInterfaceMessageReference = "MOCK-NIR-000001",
                NetworkPriority = "Normal",
                NetworkProtocol = "FIN",
                OriginalStatus = "Received",
                OwnBic = "AAAAUS33XXX",
                PossibleDuplicate = false,
                ReceiverResponder = "AAAAUS33XXX",
                ReceiverResponderBIC8 = "AAAAUS33",
                SenderRequestor = "BBBBGB22XXX",
                SenderRequestorBIC8 = "BBBBGB22",
                SequenceNumber = "000001",
                SessionNumber = "0001",
                Service = "swift.fin",
                SourceInterface = "MOCK",
                Status = "Received",
                StatusDate = messageDate,
                TouchedByHuman = false,
                UETR = "11111111-2222-4333-8444-555555555555",
                Accounts = new[] { "US1234567890", "GB0987654321" },
                Amounts = new decimal?[] { 1250.50m, 25.00m },
                Currency = new[] { "USD", "GBP" },
                BeneficiaryCustomerAccount = new[] { "US1234567890", "GB0987654321" },
                BeneficiaryCustomerBank = new[] { "AAAAUS33XXX", "CCCCGB22XXX" },
                BeneficiaryCustomerName = new[] { "Mock Beneficiary One", "Mock Beneficiary Two" },
                OrderingCustomerAccount = new[] { "DE1111111111", "DE2222222222" },
                OrderingCustomerBank = new[] { "BBBBDEFFXXX", "DDDDDEFFXXX" },
                OrderingCustomerName = new[] { "Mock Ordering One", "Mock Ordering Two" },
                SenderMessageReference = new[] { "MOCK-REFERENCE-1", "MOCK-REFERENCE-2" },
                SettlementDates = new DateTime?[] { messageDate.Date, messageDate.Date.AddDays(1) },
                TradeDealDate = new DateTime?[] { messageDate.Date.AddDays(-1), messageDate.Date },
                UnitDataOwner = new[] { "MOCK-US", "MOCK-GB" },
                ValueDate = new DateTime?[] { messageDate.Date, messageDate.Date.AddDays(1) }
            }
        };
        Errors = null;
        return true;
    }
}

internal sealed class MockSwiftMessage
{
    public string WarehouseId { get; set; }
    public DateTime? LoadedDateTime { get; set; }
    public string JSON { get; set; }
    public string Body { get; set; }
    public string BackendDirection { get; set; }
    public string CounterParty { get; set; }
    public string CounterPartyCountry { get; set; }
    public DateTime? CreationDate { get; set; }
    public string Direction { get; set; }
    public DateTime? LastModificationDate { get; set; }
    public DateTime? MessageDate { get; set; }
    public int MessageLength { get; set; }
    public string MessageFormatVersion { get; set; }
    public string MessageInputReference { get; set; }
    public string MessageType { get; set; }
    public string MessageTypeShort { get; set; }
    public string ModifiedBy { get; set; }
    public string NetworkInterfaceMessageReference { get; set; }
    public string NetworkPriority { get; set; }
    public string NetworkProtocol { get; set; }
    public string OriginalStatus { get; set; }
    public string OwnBic { get; set; }
    public bool PossibleDuplicate { get; set; }
    public string ReceiverResponder { get; set; }
    public string ReceiverResponderBIC8 { get; set; }
    public string SenderRequestor { get; set; }
    public string SenderRequestorBIC8 { get; set; }
    public string SequenceNumber { get; set; }
    public string SessionNumber { get; set; }
    public string Service { get; set; }
    public string SourceInterface { get; set; }
    public string Status { get; set; }
    public DateTime? StatusDate { get; set; }
    public bool TouchedByHuman { get; set; }
    public string UETR { get; set; }
    public IReadOnlyList<string> Accounts { get; set; } = Array.Empty<string>();
    public IReadOnlyList<decimal?> Amounts { get; set; } = Array.Empty<decimal?>();
    public IReadOnlyList<string> Currency { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BeneficiaryCustomerAccount { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BeneficiaryCustomerBank { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BeneficiaryCustomerName { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OrderingCustomerAccount { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OrderingCustomerBank { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OrderingCustomerName { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SenderMessageReference { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DateTime?> SettlementDates { get; set; } = Array.Empty<DateTime?>();
    public IReadOnlyList<DateTime?> TradeDealDate { get; set; } = Array.Empty<DateTime?>();
    public IReadOnlyList<string> UnitDataOwner { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DateTime?> ValueDate { get; set; } = Array.Empty<DateTime?>();
}
