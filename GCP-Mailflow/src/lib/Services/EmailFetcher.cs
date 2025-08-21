using Google.Apis.Gmail.v1;
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Extensions;
using DCiuve.Gcp.ExtensionDomain.Gmail;

namespace DCiuve.Gcp.Mailflow.Services;

/// <summary>
/// Service for fetching emails from Gmail.
/// </summary>
public class EmailFetcher : IDisposable
{
    private readonly IGmailClient _gmailClient;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EmailFetcher class.
    /// </summary>
    /// <param name="gmailClient">The Gmail service instance.</param>
    public EmailFetcher(IGmailClient gmailClient)
    {
        _gmailClient = gmailClient ?? throw new ArgumentNullException(nameof(gmailClient));
    }

    /// <summary>
    /// Fetches emails based on the provided filter.
    /// </summary>
    /// <param name="filter">The email filter to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of email messages matching the filter.</returns>
    public async Task<List<EmailMessage>> FetchEmailsAsync(EmailFilter filter, CancellationToken cancellationToken = default)
    {
        var request = await _gmailClient.CreateMessageListRequest("me");
        request.Q = filter.BuildQuery();
        request.MaxResults = filter.MaxResults;
        request.IncludeSpamTrash = filter.IncludeSpamTrash;
        request.PageToken = filter.PageToken;

        if (filter.LabelIds.Any())
        {
            request.LabelIds = filter.LabelIds.ToList();
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
    /// Fetchs all emails using history tracking since a specific history ID.
    /// </summary>
    /// <param name="startHistoryId">The history ID to start from.</param>
    /// <param name="filter">Optional filter to apply to found emails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of new emails since the history ID.</returns>
    public async Task<List<EmailMessage>> FetchAllSinceHistoryAsync(
        ulong startHistoryId,
        EmailFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _gmailClient.CreateHistoryListRequest("me");
        request.StartHistoryId = startHistoryId;
        request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        var response = await request.ExecuteAsync(cancellationToken);
        var newMessages = new List<EmailMessage>();

        if (response.History == null || response.History.Count < 1)
            return newMessages;

        foreach (var history in response.History)
        {
            if (history.MessagesAdded == null) continue;
            
            foreach (var messageAdded in history.MessagesAdded)
            {
                var email = await GetEmailDetailsAsync(messageAdded.Message.Id, cancellationToken);
                if (email != null && (filter == null || filter.Match(email)))
                {
                    newMessages.Add(email);
                }
            }
        }
        
        return newMessages;
    }

    /// <summary>
    /// Gets detailed information for a specific email message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The email message details, or null if not found.</returns>
    public async Task<EmailMessage?> GetEmailDetailsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var request = await _gmailClient.CreateMessageGetRequest("me", messageId);
        request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

        var message = await request.ExecuteAsync(cancellationToken);

        return message.ToEmailMessage();
    }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's email address.</returns>
    public async Task<string> GetUserEmailAsync(CancellationToken cancellationToken = default)
    {
        var profileRequest = await _gmailClient.CreateGetProfileRequest("me");
        var profile = await profileRequest.ExecuteAsync(cancellationToken);
        return profile.EmailAddress;
    }

    /// <summary>
    /// Disposes the EmailFetcher instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Note: We don't dispose _gmailService because it's injected as a dependency
            // The creator/owner of the GmailService instance is responsible for disposing it
            _disposed = true;
        }
    }
}
