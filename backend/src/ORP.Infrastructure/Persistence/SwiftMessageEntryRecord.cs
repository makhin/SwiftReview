namespace ORP.Infrastructure.Persistence;

public sealed class SwiftMessageEntryRecord
{
    public long MessageId { get; init; }
    public int Position { get; init; }
    public string? Account { get; init; }
    public string? Currency { get; init; }
    public decimal? Amount { get; init; }
    public string? BeneficiaryCustomerAccount { get; init; }
    public string? BeneficiaryCustomerBank { get; init; }
    public string? BeneficiaryCustomerName { get; init; }
    public string? OrderingCustomerAccount { get; init; }
    public string? OrderingCustomerBank { get; init; }
    public string? OrderingCustomerName { get; init; }
    public string? SenderMessageReference { get; init; }
    public DateTime? SettlementDate { get; init; }
    public DateTime? TradeDealDate { get; init; }
    public string? UnitDataOwner { get; init; }
    public DateTime? ValueDate { get; init; }
    public SwiftMessageRecord Message { get; init; } = null!;
}
