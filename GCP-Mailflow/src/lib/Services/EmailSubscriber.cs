using DCiuve.Gcp.Mailflow.Models;
using Google.Cloud.PubSub.V1;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Google.Apis.Auth.OAuth2;
using DCiuve.Gcp.Mailflow.Extensions;
using DCiuve.Gcp.ExtensionDomain.Gmail;

using historyTypes = Google.Apis.Gmail.v1.UsersResource.HistoryResource.ListRequest.HistoryTypesEnum;

namespace DCiuve.Gcp.Mailflow.Services;

/// <summary>
/// Service for managing pull subscriptions via Pub/Sub.
/// This class handles receiving and processing Gmail Pub/Sub pull messages.
/// </summary>
public class EmailSubscriber : IDisposable
{
    private readonly IGmailClient _gmailClient;
    private readonly ICredential _pubsubCredential;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EmailSubscriber class.
    /// </summary>
    /// <param name="emailFetcher">The email fetcher service.</param>
    /// <param name="gmailClient">The Gmail service for History API access.</param>
    public EmailSubscriber(IGmailClient gmailClient, ICredential pubsubCredential)
    {
        _gmailClient = gmailClient ?? throw new ArgumentNullException(nameof(gmailClient));
        _pubsubCredential = pubsubCredential ?? throw new ArgumentNullException(nameof(pubsubCredential));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts listening for pull subscription messages from Pub/Sub.
    /// Note: Requires that the Gmail watch has been set up via GmailWatchBroker first.
    /// This method does NOT create a new Gmail watch request - it assumes one already exists.
    /// </summary>
    /// <param name="projectId">The GCP project ID where the Pub/Sub subscription exists.</param>
    /// <param name="subscriptionId">The Pub/Sub subscription ID.</param>
    /// <param name="filter">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of individual emails received via pull subscription.</returns>
    public async IAsyncEnumerable<InboundMessage> StartPullSubscriptionListenerAsync(
        string projectId,
        string subscriptionId,
        EmailFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create a combined cancellation token
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token);

        var subscriberClient = await CreateSubscriberClient(projectId, subscriptionId, combinedCts.Token);
        var channel = Channel.CreateUnbounded<InboundMessage>();
        var writer = channel.Writer;

        // Local state for history ID tracking
        var sessionRef = new SessionStateRef(Guid.NewGuid().ToString());

        try
        {
            var subscriberTask = subscriberClient.StartAsync((msg, ctoken) =>
                HandlePubSubMessageAsync(sessionRef, writer, msg, filter, ctoken));

            // Yield messages from the channel as they arrive
            await foreach (var message in channel.Reader.ReadAllAsync(combinedCts.Token))
            {
                yield return message;
            }
        }
        finally
        {
            // Stop the subscriber and close the channel
            await subscriberClient.StopAsync(TimeSpan.FromSeconds(5));
            writer.Complete();
        }
    }

    private async Task<SubscriberClient.Reply> HandlePubSubMessageAsync(
        SessionStateRef sessionRef,
        ChannelWriter<InboundMessage> writer,
        PubsubMessage message,
        EmailFilter filter,
        CancellationToken cancellationToken)
    {
        var batchToken = sessionRef.GenerateNextBatchToken();

        try
        {
            var processingMessages = ProcessNotificationAsync(sessionRef, batchToken, message, filter, cancellationToken);

            await foreach (var processingTask in processingMessages)
            {
                await writer.WriteAsync(processingTask, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            var exceptionTask = Task.FromException<ProcessedMessage>(ex);
            await writer.WriteAsync(
                item: new InboundMessage(batchToken.BatchId.ToString(), exceptionTask),
                cancellationToken);
        }

        return SubscriberClient.Reply.Ack;
    }

    private async IAsyncEnumerable<InboundMessage> ProcessNotificationAsync(
        SessionStateRef sessionRef,
        BatchToken batchToken,
        PubsubMessage message,
        EmailFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var notificationData = message.Data.ToBase64();

        if (string.IsNullOrEmpty(notificationData))
        {
            yield return ErrorDataInboundMessage(
                batchToken.BatchId.ToString(),
                "Notification data cannot be null or empty.");

            yield break;
        }

        // Decode the base64 notification data
        var decodedBase64Content = Convert.FromBase64String(notificationData);
        var decodedData = System.Text.Encoding.UTF8.GetString(decodedBase64Content);

        // Parse the JSON notification (Gmail pull notification format):
        // { "emailAddress": "user@example.com", "historyId": "123456" }
        using var jsonDoc = JsonDocument.Parse(decodedData);

        if (jsonDoc.RootElement.TryGetProperty("historyId", out var historyIdElement))
        {
            var receivedHistoryId = historyIdElement.TryGetUInt64(out var historyId)
                ? historyId
                : throw new ArgumentException($"Invalid historyId format: {historyIdElement}");

            var lastHistoryId = sessionRef.LastHistoryId;
            sessionRef.UpdateLastHistoryId(receivedHistoryId);

            var fetchTasks = FetchEmailsFromHistoryAsync(batchToken, receivedHistoryId, lastHistoryId, filter, cancellationToken);
            await foreach (var msg in fetchTasks)
            {
                yield return msg;
            }
        }
    }

    private async IAsyncEnumerable<InboundMessage> FetchEmailsFromHistoryAsync(
        BatchToken batchToken,
        ulong newHistoryId,
        ulong previousHistoryId,
        EmailFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var historyRequest = await _gmailClient.CreateHistoryListRequest("me");
        historyRequest.StartHistoryId = previousHistoryId;
        historyRequest.HistoryTypesList = new List<historyTypes>()
        {
            historyTypes.MessageAdded,
        };

        var historyResponse = await historyRequest.ExecuteAsync(cancellationToken);
        if (historyResponse.History == null)
        {
            yield break; // No new history records
        }

        var boundHistory = historyResponse.History
            .Where(h => h.Id.HasValue && h.Id.Value > previousHistoryId && h.Id.Value <= newHistoryId)
            .OrderBy(h => h.Id ?? 0)
            .ToList();

        foreach (var historyRecord in boundHistory)
        {
            if (historyRecord.MessagesAdded != null && historyRecord.MessagesAdded.Count > 0)
            {
                foreach (var messageAdded in historyRecord.MessagesAdded)
                {
                    var fetchingDetailsTask = FetchFullMessageDetails(messageAdded.Message.Id, filter, cancellationToken);
                    yield return new InboundMessage(batchToken.BatchId.ToString(), fetchingDetailsTask);
                }
            }
        }
    }

    private async Task<ProcessedMessage> FetchFullMessageDetails(
        string messageId,
        EmailFilter filter,
        CancellationToken cancellationToken)
    {
        var messageRequest = await _gmailClient.CreateMessageGetRequest("me", messageId);
        var fullMessage = await messageRequest.ExecuteAsync(cancellationToken);
        var emailMessage = fullMessage.ToEmailMessage();

        if (!filter.Match(emailMessage))
        {
            return new ProcessedMessage(emailMessage, true);
        }

        return new ProcessedMessage(emailMessage, false);
    }

    private async Task<SubscriberClient> CreateSubscriberClient(
        string projectId,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        // Create the Pub/Sub subscription name
        var subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);

        // Validate that the subscription exists using SubscriberServiceApiClient
        var subscriberApi = await new SubscriberServiceApiClientBuilder()
        {
            Credential = _pubsubCredential
        }.BuildAsync(cancellationToken);

        // This validates if subscription name is correct.
        await subscriberApi.GetSubscriptionAsync(subscriptionName);

        // Create subscriber client and channel locally
        var subscriberClient = await new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            Credential = _pubsubCredential
        }.BuildAsync(cancellationToken);

        return subscriberClient;
    }

    /// <summary>
    /// Stops the pull notification listener.
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
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

    private static InboundMessage ErrorDataInboundMessage(
        string batchId,
        string errorMessage)
    {
        return new InboundMessage(
            batchId,
            Task.FromException<ProcessedMessage>(new InvalidDataException(errorMessage)));
    }
}
