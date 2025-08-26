
namespace DCiuve.Gcp.Mailflow.Models;

/// <summary>
/// Represents a processed email message.
/// </summary>
public record ProcessedMessage(EmailMessage EmailMessage, bool IsFiltered);
