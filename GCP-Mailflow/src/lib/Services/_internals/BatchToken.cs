namespace DCiuve.Gcp.Mailflow.Services;

/// <summary>
/// Value type representing a processing batch for a notification.
/// </summary>
internal readonly struct BatchToken
{
    public BatchToken(ulong batchId)
    {
        BatchId = batchId;
    }

    public ulong BatchId { get; }
}
