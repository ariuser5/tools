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

        var subscriptionRef = subscription with { };

        while (!combinedToken.IsCancellationRequested)
        {
            yield return await (subscriptionRef.LastHistoryId.HasValue
                ? CheckHistoryForNewEmailsAsync(subscriptionRef, cancellationToken)
                : _emailFetcher.FetchEmailsAsync(subscriptionRef.Filter, cancellationToken));

            var delay = TimeSpan.FromSeconds(subscription.PollingIntervalSeconds);
            await Task.Delay(delay, combinedToken);
        }
    }

    /// <summary>
    /// Polls for emails once and returns the results.
    /// Useful for one-time checks or manual polling.
    /// </summary>
    /// <param name="filter">The email filter to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching emails.</returns>
    public async Task<List<EmailMessage>> PollOnceAsync(
        EmailFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await _emailFetcher.FetchEmailsAsync(filter, cancellationToken);
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
        var (emails, _) = await PollHistoryInternalAsync(startHistoryId, filter, cancellationToken);
        return emails;
    }

    /// <summary>
    /// Checks Gmail history for new emails since the last history ID in the subscription.
    /// Updates the subscription's LastHistoryId as it processes.
    /// </summary>
    /// <param name="subscription">The subscription configuration with LastHistoryId set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of new emails found.</returns>
    private async Task<List<EmailMessage>> CheckHistoryForNewEmailsAsync(
        EmailSubscriptionParams subscription,
        CancellationToken cancellationToken)
    {
        var (emails, latestHistoryId) = await PollHistoryInternalAsync(
            subscription.LastHistoryId!.Value, 
            subscription.Filter, 
            cancellationToken);
            
        // Update the subscription's history ID with the latest we processed
        if (latestHistoryId.HasValue)
            subscription.LastHistoryId = latestHistoryId.Value;
            
        return emails;
    }

    /// <summary>
    /// Core method for polling Gmail history. Handles the common logic shared between
    /// PollSinceHistoryAsync and CheckHistoryForNewEmailsAsync.
    /// </summary>
    /// <param name="startHistoryId">The history ID to start from.</param>
    /// <param name="filter">Optional filter to apply to found emails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the emails found and the latest history ID processed.</returns>
    private async Task<(List<EmailMessage> emails, ulong? latestHistoryId)> PollHistoryInternalAsync(
        ulong startHistoryId,
        EmailFilter? filter,
        CancellationToken cancellationToken)
    {
        var request = _gmailService.Users.History.List("me");
        request.StartHistoryId = startHistoryId;
        request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        var response = await request.ExecuteAsync(cancellationToken);
        var newMessages = new List<EmailMessage>();
        ulong? latestHistoryId = null;

        if (response.History == null || response.History.Count < 1)
            return (newMessages, latestHistoryId);

        foreach (var history in response.History)
        {
            if (history.Id > startHistoryId)
                latestHistoryId = history.Id;

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
        
        return (newMessages, latestHistoryId);
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
