using System.Diagnostics.Metrics;
using ORP.Application.Abstractions;

namespace ORP.Application.Reviews;

public static class BulkApprovalMetrics
{
    public const string MeterName = "ORP.Application.Reviews";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Messages = Meter.CreateCounter<long>("orp.bulk_approve.messages", "{message}",
        "Finished message outcomes, recorded after the transaction attempt including retries.");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("orp.bulk_approve.batch.duration", "s",
        "Duration of a validated bulk request, including completed, cancelled and failed batches.");

    internal static void RecordMessage(BulkApproveMessageResult result) =>
        Messages.Add(1, new KeyValuePair<string, object?>("outcome", result.Error?.Code ?? "success"));

    internal static void RecordBatch(TimeSpan elapsed, string outcome) =>
        Duration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));
}
