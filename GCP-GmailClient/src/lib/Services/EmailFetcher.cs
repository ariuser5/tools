using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using DCiuve.Tools.Gcp.Gmail.Models;
using System.Text;

namespace DCiuve.Tools.Gcp.Gmail.Services;

/// <summary>
/// Service for fetching emails from Gmail.
/// </summary>
public class EmailFetcher : IDisposable
{
    private readonly GmailService _gmailService;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EmailFetcher class.
    /// </summary>
    /// <param name="gmailService">The Gmail service instance.</param>
    public EmailFetcher(GmailService gmailService)
    {
        _gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
    }

    /// <summary>
    /// Fetches emails based on the provided filter.
    /// </summary>
    /// <param name="filter">The email filter to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of email messages matching the filter.</returns>
    public async Task<List<EmailMessage>> FetchEmailsAsync(EmailFilter filter, CancellationToken cancellationToken = default)
    {
        var request = _gmailService.Users.Messages.List("me");
        request.Q = filter.BuildQuery();
        request.MaxResults = filter.MaxResults;
        request.IncludeSpamTrash = filter.IncludeSpamTrash;
        request.PageToken = filter.PageToken;

        if (filter.LabelIds.Any())
        {
            request.LabelIds = filter.LabelIds;
        }

        var response = await request.ExecuteAsync(cancellationToken);
        
        if (response.Messages == null || !response.Messages.Any())
        {
            return new List<EmailMessage>();
        }

        var emails = new List<EmailMessage>();
        foreach (var message in response.Messages)
        {
            var email = await GetEmailDetailsAsync(message.Id, cancellationToken);
            if (email != null)
            {
                emails.Add(email);
            }
        }

        return emails;
    }

    /// <summary>
    /// Gets detailed information for a specific email message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The email message details, or null if not found.</returns>
    public async Task<EmailMessage?> GetEmailDetailsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var request = _gmailService.Users.Messages.Get("me", messageId);
        request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

        var message = await request.ExecuteAsync(cancellationToken);
        
        return ConvertToEmailMessage(message);
    }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's email address.</returns>
    public async Task<string> GetUserEmailAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _gmailService.Users.GetProfile("me").ExecuteAsync(cancellationToken);
        return profile.EmailAddress;
    }

    /// <summary>
    /// Converts a Gmail API message to an EmailMessage model.
    /// </summary>
    /// <param name="message">The Gmail API message.</param>
    /// <returns>The converted EmailMessage.</returns>
    private EmailMessage ConvertToEmailMessage(Message message)
    {
        var emailMessage = new EmailMessage
        {
            Id = message.Id,
            ThreadId = message.ThreadId,
            Snippet = message.Snippet ?? string.Empty,
            Labels = message.LabelIds?.ToList() ?? new List<string>(),
            HistoryId = message.HistoryId,
            IsUnread = message.LabelIds?.Contains("UNREAD") ?? false
        };

        if (message.Payload?.Headers != null)
        {
            foreach (var header in message.Payload.Headers)
            {
                switch (header.Name?.ToLowerInvariant())
                {
                    case "subject":
                        emailMessage.Subject = header.Value ?? string.Empty;
                        break;
                    case "from":
                        emailMessage.From = header.Value ?? string.Empty;
                        break;
                    case "to":
                        emailMessage.To = header.Value ?? string.Empty;
                        break;
                    case "date":
                        if (DateTime.TryParse(header.Value, out var date))
                        {
                            emailMessage.Date = date;
                        }
                        break;
                }
            }
        }

        // Extract body content
        emailMessage.Body = ExtractBodyContent(message.Payload);

        return emailMessage;
    }

    /// <summary>
    /// Extracts the body content from a message payload.
    /// </summary>
    /// <param name="payload">The message payload.</param>
    /// <returns>The extracted body content.</returns>
    private string ExtractBodyContent(MessagePart? payload)
    {
        if (payload == null)
            return string.Empty;

        var body = new StringBuilder();

        // Check if this part has body data
        if (payload.Body?.Data != null)
        {
            var decodedData = Convert.FromBase64String(payload.Body.Data.Replace('-', '+').Replace('_', '/'));
            body.Append(Encoding.UTF8.GetString(decodedData));
        }

        // Recursively check parts
        if (payload.Parts != null)
        {
            foreach (var part in payload.Parts)
            {
                // Prefer plain text, but fall back to HTML
                if (part.MimeType == "text/plain" || part.MimeType == "text/html")
                {
                    var partBody = ExtractBodyContent(part);
                    if (!string.IsNullOrEmpty(partBody))
                    {
                        body.AppendLine(partBody);
                    }
                }
            }
        }

        return body.ToString().Trim();
    }

    /// <summary>
    /// Disposes the EmailFetcher instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _gmailService?.Dispose();
            _disposed = true;
        }
    }
}
