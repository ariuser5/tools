using Google.Apis.Gmail.v1;
using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Services;

/// <summary>
/// Service for polling Gmail for new emails based on filters or history tracking.
/// </summary>
public class EmailPoller : IDisposable
{
    private readonly GmailService _gmailService;
    private readonly EmailFetcher _emailFetcher;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EmailPoller class.
    /// </summary>
    /// <param name="gmailService">The Gmail service instance.</param>
    /// <param name="emailFetcher">The email fetcher service.</param>
    public EmailPoller(GmailService gmailService, EmailFetcher emailFetcher)
    {
        _gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
        _emailFetcher = emailFetcher ?? throw new ArgumentNullException(nameof(emailFetcher));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts polling for new emails based on the subscription configuration.
    /// Uses history tracking if available, otherwise falls back to filter-based polling.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of email batches.</returns>
    public async IAsyncEnumerable<List<EmailMessage>> StartPollingAsync(
        EmailSubscriptionParams subscription,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token
        ).Token;

        var filterSnapshot = subscription.Filter with { };
        var lastHistoryId = GetLastEmail(filterSnapshot, combinedToken);
        
        if (lastHistoryId == null) yield break;

        while (!combinedToken.IsCancellationRequested)
        {
            var newEmails = await PollSinceHistoryAsync(lastHistoryId.Value, filterSnapshot, combinedToken);
            lastHistoryId = newEmails.Max(email => email.HistoryId) ?? lastHistoryId;
            yield return newEmails;

            var delay = TimeSpan.FromSeconds(subscription.PollingIntervalSeconds);
            await Task.Delay(delay, combinedToken);
        }
    }

    /// <summary>
    /// Polls for emails using history tracking since a specific history ID.
    /// </summary>
    /// <param name="startHistoryId">The history ID to start from.</param>
    /// <param name="filter">Optional filter to apply to found emails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of new emails since the history ID.</returns>
    public async Task<List<EmailMessage>> PollSinceHistoryAsync(
        ulong startHistoryId,
        EmailFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var request = _gmailService.Users.History.List("me");
        request.StartHistoryId = startHistoryId;
        request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        var response = await request.ExecuteAsync(cancellationToken);
        var newMessages = new List<EmailMessage>();

        if (response.History == null || response.History.Count < 1)
            return newMessages;

        foreach (var history in response.History)
        {
            if (history.MessagesAdded != null)
            {
                foreach (var messageAdded in history.MessagesAdded)
                {
                    var email = await _emailFetcher.GetEmailDetailsAsync(messageAdded.Message.Id, cancellationToken);
                    if (email != null && (filter == null || filter.Match(email)))
                    {
                        newMessages.Add(email);
                    }
                }
            }
        }
        
        return newMessages;
    }

    private ulong? GetLastEmail(EmailFilter filter, CancellationToken cancellationToken)
    {
        // Fetch the last email based on the filter criteria
        var oneEmailFilter = filter with { MaxResults = 1 };
        var result = _emailFetcher.FetchEmailsAsync(oneEmailFilter, cancellationToken).Result;

        if (result.Count < 1)
            return 0;

        return result.Single().HistoryId;
    }

    /// <summary>
    /// Stops the polling operation.
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }
    
    /// <summary>
    /// Disposes the EmailPoller instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}
