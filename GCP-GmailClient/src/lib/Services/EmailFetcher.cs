using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using DCiuve.Tools.Gcp.Gmail.Models;
using DCiuve.Tools.Logging;
using System.Text;

namespace DCiuve.Tools.Gcp.Gmail.Services;

/// <summary>
/// Service for fetching emails from Gmail.
/// </summary>
public class EmailFetcher : IDisposable
{
    private readonly GmailService _gmailService;
    private readonly ILogger _logger;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EmailFetcher class.
    /// </summary>
    /// <param name="gmailService">The Gmail service instance.</param>
    /// <param name="logger">The logger instance.</param>
    public EmailFetcher(GmailService gmailService, ILogger logger)
    {
        _gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches emails based on the provided filter.
    /// </summary>
    /// <param name="filter">The email filter to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of email messages.</returns>
    public async Task<List<EmailMessage>> FetchEmailsAsync(EmailFilter filter, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Info($"Fetching emails with filter: {filter.BuildQuery()}");

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
                _logger.Info("No messages found matching the filter.");
                return new List<EmailMessage>();
            }

            _logger.Info($"Found {response.Messages.Count} messages. Fetching details...");

            var emails = new List<EmailMessage>();
            foreach (var message in response.Messages)
            {
                var emailMessage = await GetEmailDetailsAsync(message.Id, cancellationToken);
                if (emailMessage != null)
                {
                    emails.Add(emailMessage);
                }
            }

            _logger.Info($"Successfully fetched {emails.Count} email details.");
            return emails;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching emails: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information for a specific email message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The email message details or null if not found.</returns>
    public async Task<EmailMessage?> GetEmailDetailsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = _gmailService.Users.Messages.Get("me", messageId);
            request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

            var message = await request.ExecuteAsync(cancellationToken);
            
            return ConvertToEmailMessage(message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error getting email details for message {messageId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's email address.</returns>
    public async Task<string> GetUserEmailAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _gmailService.Users.GetProfile("me").ExecuteAsync(cancellationToken);
            return profile.EmailAddress;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error getting user email: {ex.Message}");
            throw;
        }
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
