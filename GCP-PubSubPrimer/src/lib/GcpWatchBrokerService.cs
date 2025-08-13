using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using DCiuve.Tools.Gcp.ExtensionDomain;
using Google.Apis.Http;

namespace DCiuve.Tools.Gcp.PubSub;

/// <summary>
/// Unified broker service for managing watches across multiple Google services.
/// Provides a simplified facade with consistent state management and cancellation support.
/// Each broker instance is responsible for a specific GCP OAuth identity.
/// </summary>
public class GcpWatchBrokerService : IGcpExtensionService, IDisposable
{
	
    private readonly IConfigurableHttpClientInitializer _httpClientInitializer;
    private readonly string _applicationName;
    private readonly WatchStateManager _stateManager;
    private readonly Dictionary<string, IDisposable> _services = new();
    private bool _disposed;

    /// <summary>
    /// Gets the application name used for Google API requests.
    /// </summary>
    public string ApplicationName => _applicationName;

    public GcpWatchBrokerService(
		IConfigurableHttpClientInitializer httpClientInitializer,
		string applicationName)
    {
        _httpClientInitializer = httpClientInitializer ?? throw new ArgumentNullException(nameof(httpClientInitializer));
        _applicationName = applicationName ?? throw new ArgumentException("Application name cannot be null or empty", nameof(applicationName));
        _stateManager = new WatchStateManager(applicationName);
    }

    #region Gmail Watch Methods

    /// <summary>
    /// Creates or retrieves a Gmail watch for inbox notifications.
    /// </summary>
    /// <param name="topicName">The Pub/Sub topic to publish notifications to.</param>
    /// <param name="labelIds">Optional label IDs to filter messages. Default is ["INBOX"].</param>
    /// <param name="forceNew">If true, creates a new watch even if one exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Watch result with Gmail watch response and cancel action.</returns>
    public async Task<WatchResult<WatchResponse>> WatchGmailAsync(
        string topicName,
        string[]? labelIds = null,
        bool forceNew = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(topicName))
            throw new ArgumentException("Topic name cannot be null or empty", nameof(topicName));

        // Check for existing active watch unless forced to create new
        if (!forceNew)
        {
            var existingWatch = await _stateManager.GetActiveWatchStateAsync(Constants.GmailServiceTypeName);
            if (existingWatch != null && existingWatch.TopicName == topicName)
            {
                // Return existing watch as WatchResult
                var existingResponse = new WatchResponse
                {
                    HistoryId = ulong.TryParse(existingWatch.WatchId, out var id) ? id : null,
                    Expiration = new DateTimeOffset(existingWatch.Expiration).ToUnixTimeMilliseconds()
                };

                return new WatchResult<WatchResponse>(
                    Constants.GmailServiceTypeName,
                    existingResponse,
                    topicName,
                    existingWatch.Expiration,
                    existingWatch.CreatedAt,
                    isNewlyCreated: false,
                    existingWatch.WatchId,
                    async ct => await StopGmailWatchAsync(ct));
            }
        }

        var gmailService = GetOrCreateGmailService();
        var watchRequest = new WatchRequest
        {
            LabelFilterAction = "include",
            TopicName = topicName,
            LabelIds = labelIds ?? new[] { "INBOX" }
        };

        var request = gmailService.Users.Watch(watchRequest, "me");
        var response = await request.ExecuteAsync(cancellationToken);

        // Save the watch state
        var expiration = response.Expiration.HasValue 
            ? DateTimeOffset.FromUnixTimeMilliseconds(response.Expiration.Value).DateTime
            : DateTime.UtcNow.AddDays(Constants.Gmail.DefaultExpirationDays);

        var createdAt = DateTime.UtcNow;
        await _stateManager.SaveWatchStateAsync(
            Constants.GmailServiceTypeName,
            response.HistoryId?.ToString() ?? "0",
            expiration,
            topicName,
            new Dictionary<string, object>
            {
                ["labelIds"] = labelIds ?? new[] { "INBOX" }
            });

        return new WatchResult<WatchResponse>(
            Constants.GmailServiceTypeName,
            response,
            topicName,
            expiration,
            createdAt,
            isNewlyCreated: true,
            response.HistoryId?.ToString(),
            async ct => await StopGmailWatchAsync(ct));
    }

    /// <summary>
    /// Stops the Gmail watch.
    /// </summary>
    public async Task<bool> StopGmailWatchAsync(CancellationToken cancellationToken = default)
    {
        const string serviceType = Constants.GmailServiceTypeName;
        
        try
        {
            var gmailService = GetOrCreateGmailService();
            await gmailService.Users.Stop("me").ExecuteAsync(cancellationToken);
            await _stateManager.ClearWatchStateAsync(serviceType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Drive Watch Methods

    /// <summary>
    /// Creates or retrieves a Drive watch for file change notifications.
    /// </summary>
    /// <param name="webhookUrl">The webhook URL to receive notifications (Drive uses webhooks, not Pub/Sub).</param>
    /// <param name="forceNew">If true, creates a new watch even if one exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Watch result with Drive channel response and cancel action.</returns>
    public Task<WatchResult<object>> WatchDriveAsync(
        string webhookUrl,
        bool forceNew = false,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Drive watch functionality is not yet implemented.");
    }

    /// <summary>
    /// Stops a specific Drive watch by channel ID.
    /// </summary>
    public Task<bool> StopDriveWatchAsync(string channelId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Drive watch functionality is not yet implemented.");
    }

    #endregion

    #region Calendar Watch Methods

    /// <summary>
    /// Creates or retrieves a Calendar watch for event notifications.
    /// </summary>
    /// <param name="webhookUrl">The webhook URL to receive notifications.</param>
    /// <param name="calendarId">The calendar ID to watch. Default is "primary".</param>
    /// <param name="forceNew">If true, creates a new watch even if one exists.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Watch result with Calendar channel response and cancel action.</returns>
    public Task<WatchResult<object>> WatchCalendarAsync(
        string webhookUrl,
        string calendarId = "primary",
        bool forceNew = false,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Calendar watch functionality is not yet implemented.");
    }

    /// <summary>
    /// Stops a specific Calendar watch by channel ID.
    /// </summary>
    public Task<bool> StopCalendarWatchAsync(string channelId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Calendar watch functionality is not yet implemented.");
    }

    #endregion

    #region Service Management

    /// <summary>
    /// Gets all currently active watches across all services.
    /// </summary>
    public async Task<Dictionary<string, WatchState>> GetAllActiveWatchesAsync()
    {
        return await _stateManager.GetAllActiveWatchesAsync();
    }

    /// <summary>
    /// Stops all active watches across all services.
    /// </summary>
    public async Task<Dictionary<string, bool>> StopAllWatchesAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();
        var activeWatches = await GetAllActiveWatchesAsync();

        foreach (var watch in activeWatches)
        {
            bool success = watch.Key switch
            {
                Constants.GmailServiceTypeName => await StopGmailWatchAsync(cancellationToken),
                Constants.DriveServiceTypeName => throw new NotImplementedException("Drive watch functionality is not yet implemented."),
                Constants.CalendarServiceTypeName => throw new NotImplementedException("Calendar watch functionality is not yet implemented."),
                _ => false
            };
            results[watch.Key] = success;
        }

        return results;
    }

    private GmailService GetOrCreateGmailService()
    {
        const string key = Constants.GmailServiceTypeName;
        if (!_services.ContainsKey(key))
        {
            _services[key] = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _httpClientInitializer,
                ApplicationName = _applicationName
            });
        }
        return (GmailService)_services[key];
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var service in _services.Values)
            {
                service?.Dispose();
            }
            _services.Clear();
            _disposed = true;
        }
    }

    #endregion
}
