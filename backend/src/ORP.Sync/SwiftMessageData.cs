using System;
using System.Collections.Generic;

namespace ORP.Sync;

internal sealed class SwiftMessageData
{
    public string WarehouseId { get; set; }
    public DateTime? LoadedDateTime { get; set; }
    public string Json { get; set; }
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
    public string ReceiverResponderBic8 { get; set; }
    public string SenderRequestor { get; set; }
    public string SenderRequestorBic8 { get; set; }
    public string SequenceNumber { get; set; }
    public string SessionNumber { get; set; }
    public string Service { get; set; }
    public string SourceInterface { get; set; }
    public string Status { get; set; }
    public DateTime? StatusDate { get; set; }
    public bool TouchedByHuman { get; set; }
    public string Uetr { get; set; }
    public IReadOnlyList<string> Accounts { get; set; } = Array.Empty<string>();
    public IReadOnlyList<decimal?> Amounts { get; set; } = Array.Empty<decimal?>();
    public IReadOnlyList<string> Currencies { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BeneficiaryCustomerAccounts { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BeneficiaryCustomerBanks { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BeneficiaryCustomerNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OrderingCustomerAccounts { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OrderingCustomerBanks { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OrderingCustomerNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SenderMessageReferences { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DateTime?> SettlementDates { get; set; } = Array.Empty<DateTime?>();
    public IReadOnlyList<DateTime?> TradeDealDates { get; set; } = Array.Empty<DateTime?>();
    public IReadOnlyList<string> UnitDataOwners { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DateTime?> ValueDates { get; set; } = Array.Empty<DateTime?>();

    public int EntryCount => Math.Max(Math.Max(Math.Max(Math.Max(Accounts.Count, Amounts.Count), Currencies.Count),
        Math.Max(Math.Max(BeneficiaryCustomerAccounts.Count, BeneficiaryCustomerBanks.Count), BeneficiaryCustomerNames.Count)),
        Math.Max(Math.Max(Math.Max(OrderingCustomerAccounts.Count, OrderingCustomerBanks.Count), OrderingCustomerNames.Count),
            Math.Max(Math.Max(SenderMessageReferences.Count, SettlementDates.Count),
                Math.Max(Math.Max(TradeDealDates.Count, UnitDataOwners.Count), ValueDates.Count))));

    public IReadOnlyList<int> CollectionLengths => new[]
    {
        Accounts.Count, Amounts.Count, Currencies.Count,
        BeneficiaryCustomerAccounts.Count, BeneficiaryCustomerBanks.Count, BeneficiaryCustomerNames.Count,
        OrderingCustomerAccounts.Count, OrderingCustomerBanks.Count, OrderingCustomerNames.Count,
        SenderMessageReferences.Count, SettlementDates.Count, TradeDealDates.Count, UnitDataOwners.Count,
        ValueDates.Count
    };
}
