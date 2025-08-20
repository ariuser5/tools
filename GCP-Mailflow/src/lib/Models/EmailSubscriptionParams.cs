namespace DCiuve.Gcp.Mailflow.Models;

/// <summary>
/// Represents an email subscription configuration.
/// </summary>
public record EmailSubscriptionParams
{
    /// <summary>
    /// Gets or sets the subscription name/identifier.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the Cloud Pub/Sub topic name for push notifications.
    /// </summary>
    public string TopicName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter to apply to the subscription.
    /// </summary>
    public EmailFilter Filter { get; init; } = new();

    /// <summary>
    /// Gets or sets the callback URL for webhook notifications.
    /// </summary>
    public string? CallbackUrl { get; init; }

    /// <summary>
    /// Gets or sets the polling interval in seconds for checking new emails.
    /// </summary>
    public int PollingIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Gets or sets the end time for the subscription. If null, the subscription runs indefinitely.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}
