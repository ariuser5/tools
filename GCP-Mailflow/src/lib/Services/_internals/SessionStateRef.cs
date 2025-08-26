namespace DCiuve.Gcp.Mailflow.Services;

/// <summary>
/// Simple container for tracking history state functionally and safely across threads.
/// </summary>
internal class SessionStateRef
{
    private ulong _currentBatchId = 0;
    private ulong _lastHistoryId = 0;
    private readonly object _historyIdLock = new();

    public SessionStateRef(string sessionId, ulong initialHistoryId = 0)
    {
        SessionId = sessionId;
        _lastHistoryId = initialHistoryId;
    }

    /// <summary>The unique identifier for the session. </summary>
    public string SessionId { get; set; }

    /// <summary>Gets the current batch token.</summary>
    public BatchToken CurrentBatchToken => new(_currentBatchId);

    /// <summary>Gets the last processed history id.</summary>
    public ulong LastHistoryId
    {
        get { lock (_historyIdLock) { return _lastHistoryId; } }
    }

    /// <summary>Gets the next batch token for a new notification.</summary>
    public BatchToken GenerateNextBatchToken()
    {
        var batchId = GetNextBatchId();
        return new BatchToken(batchId);
    }

    /// <summary>
    /// Thread-safe update of the last processed history id.
    /// Only updates if the new value is greater than the current value.
    /// </summary>
    public void UpdateLastHistoryId(ulong candidateHistoryId)
    {
        lock (_historyIdLock)
        {
            if (candidateHistoryId > _lastHistoryId)
            {
                _lastHistoryId = candidateHistoryId;
            }
        }
    }

    private ulong GetNextBatchId()
    {
        return Interlocked.Increment(ref _currentBatchId);
    }
}
