using Google.Apis.Gmail.v1.Data;
using DCiuve.Gcp.PubSub;
using DCiuve.Gcp.ExtensionDomain.Gmail;

namespace DCiuve.Gcp.Mailflow.Services;

/// <summary>
/// Helper class for Gmail watch operations using PubSubPrimer library.
/// </summary>
public class GmailWatchBroker : IDisposable
{
    private readonly GcpWatchBrokerService _watchBrokerService;
    private readonly WatchStateManager _stateManager;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the GmailWatchBroker class.
    /// </summary>
    /// <param name="gmailClient">The Gmail client instance.</param>
    /// <param name="applicationName">The application name used for state management.</param>
    public GmailWatchBroker(IGmailClient gmailClient, string? applicationName = null)
    {
        var appName = applicationName ?? AppDomain.CurrentDomain.FriendlyName;
        _watchBrokerService = new GcpWatchBrokerService(
            gmailClient?.HttpClientInitializer ?? throw new ArgumentNullException(nameof(gmailClient)), 
            appName);
        _stateManager = new WatchStateManager(appName);
    }
    /// <summary>
    /// Sets up a Gmail pull/push notification subscription using the PubSubPrimer library.
    /// </summary>
    /// <param name="topicName">The Pub/Sub topic name.</param>
    /// <param name="labelIds">Optional label IDs to filter messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Gmail watch response.</returns>
    public async Task<WatchResponse> SendWatchRequestAsync(
        string topicName,
        string[]? labelIds = null, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GmailWatchBroker));
        
        var watchResult = await _watchBrokerService.WatchGmailAsync(topicName, labelIds, forceNew: true, cancellationToken);
        return watchResult.Response;
    }

    /// <summary>
    /// Stops the Gmail pul/push notification subscription using the PubSubPrimer library.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the watch was successfully stopped; otherwise, false.</returns>
    public async Task<bool> StopPullNotificationsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GmailWatchBroker));
        
        return await _watchBrokerService.StopGmailWatchAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the current active Gmail watch state.
    /// </summary>
    /// <returns>The current watch state if active, otherwise null.</returns>
    public async Task<WatchState?> GetActiveWatchStateAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GmailWatchBroker));
        
        return await _stateManager.GetActiveWatchStateAsync("gmail");
    }

    /// <summary>
    /// Checks if there's an active Gmail watch.
    /// </summary>
    /// <returns>True if there's an active watch, otherwise false.</returns>
    public async Task<bool> IsWatchActiveAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GmailWatchBroker));
        
        return await _stateManager.IsWatchActiveAsync("gmail");
    }

    /// <summary>
    /// Clears the Gmail watch state.
    /// NOTE: Do not call this method while a watch is active.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearWatchStateAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GmailWatchBroker));
        
        await _stateManager.ClearWatchStateAsync("gmail");
    }

    /// <summary>
    /// Disposes the resources used by the GmailWatchBroker.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _watchBrokerService?.Dispose();
            _disposed = true;
        }
    }
}
