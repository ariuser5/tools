namespace DCiuve.Tools.Gcp.PubSub;

/// <summary>
/// Unified wrapper for all Google service watch responses.
/// </summary>
public class WatchResult<T> where T : class
{
    /// <summary>
    /// The service type that created this watch (gmail, drive, calendar, etc.).
    /// </summary>
    public string ServiceType { get; }

    /// <summary>
    /// The original watch response from the Google API.
    /// </summary>
    public T Response { get; }

    /// <summary>
    /// The topic name this watch publishes to.
    /// </summary>
    public string TopicName { get; }

    /// <summary>
    /// When this watch expires (UTC).
    /// </summary>
    public DateTime? Expiration { get; }

    /// <summary>
    /// When this watch was originally created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Whether this watch was created by this call (true) or was already existing (false).
    /// </summary>
    public bool IsNewlyCreated { get; }

    /// <summary>
    /// Unique identifier for this watch (varies by service type).
    /// </summary>
    public string? WatchId { get; }

    /// <summary>
    /// Function to cancel/stop this specific watch.
    /// </summary>
    public Func<CancellationToken, Task<bool>> CancelAction { get; }

    public WatchResult(
        string serviceType,
        T response,
        string topicName,
        DateTime? expiration,
        DateTime createdAt,
        bool isNewlyCreated,
        string? watchId,
        Func<CancellationToken, Task<bool>> cancelAction)
    {
        ServiceType = serviceType;
        Response = response;
        TopicName = topicName;
        Expiration = expiration;
        CreatedAt = createdAt;
        IsNewlyCreated = isNewlyCreated;
        WatchId = watchId;
        CancelAction = cancelAction;
    }

    /// <summary>
    /// Cancels this watch.
    /// </summary>
    public Task<bool> CancelAsync(CancellationToken cancellationToken = default)
        => CancelAction(cancellationToken);

    /// <summary>
    /// Checks if this watch is still active (not expired).
    /// </summary>
    public bool IsActive => Expiration == null || Expiration > DateTime.UtcNow.AddMinutes(5);
}
