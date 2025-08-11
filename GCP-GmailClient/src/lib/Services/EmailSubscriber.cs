using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using DCiuve.Tools.Gcp.Gmail.Models;

namespace DCiuve.Tools.Gcp.Gmail.Services;

/// <summary>
/// Service for managing email subscriptions and monitoring new emails.
/// </summary>
public class EmailSubscriber : IDisposable
{
    private readonly GmailService _gmailService;
    private readonly EmailFetcher _emailFetcher;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;

    /// <summary>
    /// Event triggered when new emails are received.
    /// </summary>
    public event EventHandler<List<EmailMessage>>? NewEmailsReceived;

    /// <summary>
    /// Initializes a new instance of the EmailSubscriber class.
    /// </summary>
    /// <param name="gmailService">The Gmail service instance.</param>
    /// <param name="emailFetcher">The email fetcher service.</param>
    public EmailSubscriber(GmailService gmailService, EmailFetcher emailFetcher)
    {
        _gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
        _emailFetcher = emailFetcher ?? throw new ArgumentNullException(nameof(emailFetcher));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Sets up a Gmail push notification subscription.
    /// This creates a new Gmail watch request.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The watch response from Gmail API.</returns>
    public async Task<WatchResponse> SetupPushNotificationAsync(EmailSubscription subscription, CancellationToken cancellationToken = default)
    {
        var watchRequest = new WatchRequest
        {
            TopicName = subscription.TopicName,
            LabelIds = subscription.Filter.LabelIds.Any() ? subscription.Filter.LabelIds : null,
            LabelFilterAction = "include"
        };

        var request = _gmailService.Users.Watch(watchRequest, "me");
        var response = await request.ExecuteAsync(cancellationToken);

        subscription.LastHistoryId = response.HistoryId;
        subscription.LastUpdated = DateTime.UtcNow;

        return response;
    }

    /// <summary>
    /// Stops the Gmail push notification subscription.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StopPushNotificationAsync(CancellationToken cancellationToken = default)
    {
        var request = _gmailService.Users.Stop("me");
        await request.ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Starts listening for push notifications from Pub/Sub.
    /// Note: Requires that the Gmail watch has been set up via PubSubPrimer first.
    /// This method does NOT create a new Gmail watch request - it assumes one already exists.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartPushNotificationListenerAsync(EmailSubscription subscription, CancellationToken cancellationToken = default)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        // For push notifications, we mainly wait and let the Pub/Sub system
        // deliver notifications to our webhook endpoint
        // This method serves as a placeholder for future webhook integration
        while (!combinedToken.IsCancellationRequested && subscription.IsActive)
        {
            // In a real implementation, this would integrate with a webhook receiver
            // For now, we'll do periodic checks as a fallback
            await CheckForNewEmailsAsync(subscription, combinedToken);
            await Task.Delay(TimeSpan.FromMinutes(1), combinedToken); // Much less frequent than polling
        }
    }

    /// <summary>
    /// Starts polling for new emails based on the subscription.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartPollingAsync(EmailSubscription subscription, CancellationToken cancellationToken = default)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token).Token;

        while (!combinedToken.IsCancellationRequested && subscription.IsActive)
        {
            await CheckForNewEmailsAsync(subscription, combinedToken);
            await Task.Delay(TimeSpan.FromSeconds(subscription.PollingIntervalSeconds), combinedToken);
        }
    }

    /// <summary>
    /// Checks for new emails since the last history ID.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CheckForNewEmailsAsync(EmailSubscription subscription, CancellationToken cancellationToken = default)
    {
        if (subscription.LastHistoryId.HasValue)
        {
            await CheckHistoryForNewEmailsAsync(subscription, cancellationToken);
        }
        else
        {
            await CheckAllEmailsAsync(subscription, cancellationToken);
        }
    }

    /// <summary>
    /// Checks Gmail history for new emails since the last history ID.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task CheckHistoryForNewEmailsAsync(EmailSubscription subscription, CancellationToken cancellationToken)
    {
        var request = _gmailService.Users.History.List("me");
        request.StartHistoryId = subscription.LastHistoryId;
        request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;

        var response = await request.ExecuteAsync(cancellationToken);

        if (response.History == null || !response.History.Any())
        {
            return;
        }

        var newMessages = new List<EmailMessage>();
        var latestHistoryId = subscription.LastHistoryId;

        foreach (var history in response.History)
        {
            if (history.Id > latestHistoryId)
            {
                latestHistoryId = history.Id;
            }

            if (history.MessagesAdded != null)
            {
                foreach (var messageAdded in history.MessagesAdded)
                {
                    var email = await _emailFetcher.GetEmailDetailsAsync(messageAdded.Message.Id, cancellationToken);
                    if (email != null && DoesEmailMatchFilter(email, subscription.Filter))
                    {
                        newMessages.Add(email);
                    }
                }
            }
        }

        if (newMessages.Any())
        {
            NewEmailsReceived?.Invoke(this, newMessages);
        }

        subscription.LastHistoryId = latestHistoryId;
        subscription.LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks all emails matching the filter (used when no history ID is available).
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task CheckAllEmailsAsync(EmailSubscription subscription, CancellationToken cancellationToken)
    {
        var emails = await _emailFetcher.FetchEmailsAsync(subscription.Filter, cancellationToken);
        
        if (emails.Any())
        {
            NewEmailsReceived?.Invoke(this, emails);
            
            // Update the history ID to the latest message
            var latestHistoryId = emails.Max(e => e.HistoryId);
            subscription.LastHistoryId = latestHistoryId;
            subscription.LastUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Checks if an email matches the subscription filter.
    /// </summary>
    /// <param name="email">The email to check.</param>
    /// <param name="filter">The filter to apply.</param>
    /// <returns>True if the email matches the filter.</returns>
    private bool DoesEmailMatchFilter(EmailMessage email, EmailFilter filter)
    {
        if (filter.UnreadOnly && !email.IsUnread)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.FromEmail) && 
            !email.From.Contains(filter.FromEmail, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Subject) && 
            !email.Subject.Contains(filter.Subject, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.DateStart.HasValue && email.Date < filter.DateStart.Value)
        {
            return false;
        }

        if (filter.DateEnd.HasValue && email.Date > filter.DateEnd.Value)
        {
            return false;
        }

        if (filter.LabelIds.Any() && !filter.LabelIds.Any(labelId => email.Labels.Contains(labelId)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Stops the polling operation.
    /// </summary>
    public void StopPolling()
    {
        _cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Disposes the EmailSubscriber instance.
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
