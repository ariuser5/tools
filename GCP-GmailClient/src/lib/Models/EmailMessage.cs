namespace DCiuve.Tools.Gcp.Gmail.Models;

/// <summary>
/// Represents an email message with simplified properties.
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the thread ID.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient's email address.
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the email was received.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the snippet (preview) of the email content.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the labels associated with this email.
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the email is unread.
    /// </summary>
    public bool IsUnread { get; set; }

    /// <summary>
    /// Gets or sets the email body content.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the history ID for this message.
    /// </summary>
    public ulong? HistoryId { get; set; }
}
