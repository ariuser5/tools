namespace DCiuve.Tools.Gcp.Gmail.Models;

/// <summary>
/// Represents an email subscription configuration.
/// </summary>
public class EmailSubscription
{
    /// <summary>
    /// Gets or sets the Cloud Pub/Sub topic name for push notifications.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter to apply to the subscription.
    /// </summary>
    public EmailFilter Filter { get; set; } = new();

    /// <summary>
    /// Gets or sets the callback URL for webhook notifications.
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Gets or sets the polling interval in seconds for checking new emails.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether the subscription is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the last history ID processed.
    /// </summary>
    public ulong? LastHistoryId { get; set; }

    /// <summary>
    /// Gets or sets the subscription name/identifier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
