using DCiuve.Tools.Gcp.ExtensionDomain;
using DCiuve.Tools.Gcp.Mailflow.Services;
using DCiuve.Tools.Gcp.PubSub;
using DCiuve.Tools.Logging;
using Google.Apis.Gmail.v1;

namespace DCiuve.Tools.Gcp.Mailflow.Cli.Services;

/// <summary>
/// Manages Gmail watch lifecycle including creation, renewal, and expiration handling.
/// </summary>
public class GmailWatchManager(
    GmailService gmailService,
    ILogger logger,
    string applicationName = "My Gmail Client CLI"
) : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the GmailWatchManager class.
    /// </summary>
    private const int ScheduleWatchRetryMinutes = 5;

    /// <summary>
    /// The threshold for renewing the watch before it expires.
    /// </summary>
    private const int WatchRenewalThresholdMinutes = 20;

    /// <summary>
    /// The advance time to renew the watch before it expires.
    /// </summary>
    private const int WatchRenewalAdvanceMinutes = 15;
    
	private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly GmailWatchBroker _watchBroker = new(gmailService, applicationName);
    private Timer? _renewalTimer;
    private bool _disposed;
    private bool _owningWatch;
    private string? _ownedWatchId;

	/// <summary>
	/// Starts managing Gmail watch lifecycle for the specified duration.
	/// </summary>
	/// <param name="topicName">The Pub/Sub topic name for notifications.</param>
	/// <param name="labelIds">Optional label IDs to filter emails.</param>
	/// <param name="endTime">When to stop managing watches (null for indefinite).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task StartWatchManagementAsync(
        string topicName,
        string[]? labelIds = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(GmailWatchManager));

        _logger.Info("Starting Gmail watch management...");

        try
        {
            await EnsureActiveWatchAsync(topicName, labelIds, endTime, cancellationToken);
            await ScheduleNextRenewalCheckAsync(topicName, labelIds, endTime, cancellationToken);

            // Handle duration-based or indefinite operation
            if (endTime.HasValue)
            {
                _logger.Info($"Watch management will continue until: {endTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
                
                var remainingTime = endTime.Value - DateTime.UtcNow;
                if (remainingTime > TimeSpan.Zero)
                    await Task.Delay(remainingTime, cancellationToken);
            }
            else
            {
                _logger.Info("Watch management will continue indefinitely (until cancellation).");
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Gmail watch management cancelled.");
        }
        finally
        {
            await StopWatchManagementAsync();
        }
    }

    /// <summary>
    /// Stops watch management and cleans up resources.
    /// If we own the watch, it will be cancelled.
    /// </summary>
    public async Task StopWatchManagementAsync()
    {
        _renewalTimer?.Dispose();
        _renewalTimer = null;
        
        // Cancel the watch if we own it
        if (_owningWatch && !string.IsNullOrEmpty(_ownedWatchId))
        {
            _logger.Info("Cancelling Gmail watch since we own it...");
            try
            {
                var stopped = await _watchBroker.StopPushNotificationsAsync();
                if (stopped)
                {
                    await _watchBroker.ClearWatchStateAsync();
                    _logger.Info("Owned Gmail watch cancelled successfully.");
                }
                else
                {
                    _logger.Warning("Failed to stop owned Gmail watch.");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to cancel owned Gmail watch: {ex.Message}");
            }
        }
        else if (_owningWatch)
        {
            _logger.Info("We own the watch but no watch ID available to cancel.");
        }
        else
        {
            _logger.Info("Using existing watch, leaving it active.");
        }
        
        _logger.Info("Gmail watch management stopped.");
    }

    /// <summary>
    /// Gets the current active watch state.
    /// </summary>
    /// <returns>The current watch state if active, otherwise null.</returns>
    public async Task<WatchState?> GetActiveWatchStateAsync()
    {
        return await _watchBroker.GetActiveWatchStateAsync();
    }

    /// <summary>
    /// Ensures there's an active Gmail watch, creating one if necessary.
    /// </summary>
    private async Task EnsureActiveWatchAsync(
        string topicName,
        string[]? labelIds,
        DateTime? endTime,
        CancellationToken cancellationToken)
    {
        _logger.Debug("Checking for existing Gmail watch...");
        var existingWatch = await _watchBroker.GetActiveWatchStateAsync();

        if (existingWatch != null && existingWatch.Expiration > DateTime.UtcNow)
        {
            _logger.Info($"Active watch found (expires: {existingWatch.Expiration:yyyy-MM-dd HH:mm:ss} UTC).");
            _logger.Debug($"Watch details - Watch ID: {existingWatch.WatchId}, Topic: {existingWatch.TopicName}");
            
            // We're using an existing watch, so we don't own it
            _owningWatch = false;
            _ownedWatchId = null;

            // Check if current watch will expire before our end time
            if (endTime.HasValue && existingWatch.Expiration < endTime.Value)
            {
                var timeUntilExpiration = existingWatch.Expiration - DateTime.UtcNow;
                _logger.Info($"Current watch expires before subscription ends. Will renew in {timeUntilExpiration:hh\\:mm\\:ss}.");
            }
            else if (!endTime.HasValue)
            {
                var timeUntilExpiration = existingWatch.Expiration - DateTime.UtcNow;
                _logger.Info($"Will renew watch in {timeUntilExpiration:hh\\:mm\\:ss} (indefinite subscription).");
            }
        }
        else
        {
            if (existingWatch != null)
            {
                _logger.Info($"Found expired watch (expired: {existingWatch.Expiration:yyyy-MM-dd HH:mm:ss} UTC). Creating new watch...");
            }
            else
            {
                _logger.Info("No active watch found. Creating new Gmail watch...");
            }

            // We will be creating and owning a new watch
            _owningWatch = true;
            await CreateNewWatchAsync(topicName, labelIds, cancellationToken);
        }
    }

    /// <summary>
    /// Creates a new Gmail watch and saves its state.
    /// </summary>
    private async Task CreateNewWatchAsync(
        string topicName,
        string[]? labelIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var watchResponse = await _watchBroker.SendWatchRequestAsync(
                topicName: topicName,
                labelIds: labelIds,
                cancellationToken: cancellationToken);

            // Convert epoch milliseconds to DateTime
            var expirationDateTime = watchResponse.Expiration.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(watchResponse.Expiration.Value).DateTime
                : (DateTime?)null;

            var expirationDisplay = expirationDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

            _logger.Info($"Gmail watch created successfully. History ID: {watchResponse.HistoryId}, Expiration: {expirationDisplay} UTC");

            // Get the watch ID from the state that was just saved
            var newWatchState = await _watchBroker.GetActiveWatchStateAsync();
            if (newWatchState != null)
            {
                _ownedWatchId = newWatchState.WatchId;
                _logger.Debug($"Stored owned watch ID for cancellation: {_ownedWatchId}");
            }

            if (expirationDateTime.HasValue)
            {
                var timeUntilExpiration = expirationDateTime.Value - DateTime.UtcNow;
                _logger.Debug($"Watch will expire in {timeUntilExpiration:d\\.hh\\:mm\\:ss}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create Gmail watch: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Schedules the next renewal check based on when the current watch expires.
    /// </summary>
    private async Task ScheduleNextRenewalCheckAsync(
        string topicName,
        string[]? labelIds,
        DateTime? endTime,
        CancellationToken cancellationToken)
    {
        var currentWatch = await _watchBroker.GetActiveWatchStateAsync();
        if (currentWatch == null)
        {
            _logger.Warning("No active watch found for scheduling renewal. Will check again in 5 minutes.");
            var delay = TimeSpan.FromMinutes(ScheduleWatchRetryMinutes);
            ScheduleRenewalTimer(delay, topicName, labelIds, endTime, cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        var timeUntilExpiration = currentWatch.Expiration - now;
        
        // Schedule renewal check 15 minutes before expiration (but at least 1 minute from now)
        var renewalAdvanceTime = TimeSpan.FromMinutes(WatchRenewalAdvanceMinutes);
        var timeUntilRenewal = timeUntilExpiration - renewalAdvanceTime;
        
        // Ensure we don't schedule in the past or too soon
        if (timeUntilRenewal <= TimeSpan.FromMinutes(1))
        {
            timeUntilRenewal = TimeSpan.FromMinutes(1);
            _logger.Info($"Watch expires soon (in {timeUntilExpiration:hh\\:mm\\:ss}). Scheduling immediate renewal check.");
        }
        else
        {
            _logger.Info($"Watch expires in {timeUntilExpiration:d\\.hh\\:mm\\:ss}. Scheduling renewal check in {timeUntilRenewal:d\\.hh\\:mm\\:ss}.");
        }

        ScheduleRenewalTimer(timeUntilRenewal, topicName, labelIds, endTime, cancellationToken);
    }

    /// <summary>
    /// Schedules a single-shot timer for renewal check.
    /// </summary>
    private void ScheduleRenewalTimer(
        TimeSpan delay,
        string topicName,
        string[]? labelIds,
        DateTime? endTime,
        CancellationToken cancellationToken)
    {
        _renewalTimer?.Dispose();
        _renewalTimer = new Timer(
            callback: async _ => await CheckAndRenewWatchAsync(topicName, labelIds, endTime, cancellationToken),
            state: null,
            dueTime: delay,
            period: Timeout.InfiniteTimeSpan // Single-shot timer
        );
    }

    /// <summary>
    /// Checks if the current watch needs renewal and creates a new one if necessary.
    /// </summary>
    private async Task CheckAndRenewWatchAsync(
        string topicName,
        string[]? labelIds,
        DateTime? endTime,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var currentWatch = await _watchBroker.GetActiveWatchStateAsync();
            if (currentWatch == null)
            {
                _logger.Warning("No active watch found during renewal check. Creating new watch...");
                await CreateNewWatchAsync(topicName, labelIds, cancellationToken);
                // Schedule next check after creating new watch
                await ScheduleNextRenewalCheckAsync(topicName, labelIds, endTime, cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            var timeUntilExpiration = currentWatch.Expiration - now;
            
            // Renew if watch expires within the next 20 minutes
            var renewalThreshold = TimeSpan.FromMinutes(WatchRenewalThresholdMinutes);
            
            if (timeUntilExpiration <= renewalThreshold)
            {
                _logger.Info($"Watch expires in {timeUntilExpiration:hh\\:mm\\:ss}. Renewing now...");
                
                // Check if we should continue renewing based on end time
                if (endTime.HasValue)
                {
                    var newWatchExpiration = now.Add(TimeSpan.FromDays(Constants.Gmail.DefaultExpirationDays));
                    if (newWatchExpiration > endTime.Value)
                    {
                        var remainingDuration = endTime.Value - now;
                        if (remainingDuration <= TimeSpan.Zero)
                        {
                            _logger.Info("Subscription duration has ended. Not renewing watch.");
                            return;
                        }
                        _logger.Info($"Creating final watch for remaining duration: {remainingDuration:d\\.hh\\:mm\\:ss}");
                    }
                }

                // Mark that we're creating and owning a new watch during renewal
                _owningWatch = true;
                await CreateNewWatchAsync(topicName, labelIds, cancellationToken);
                
                // Schedule the next renewal check for the new watch
                await ScheduleNextRenewalCheckAsync(topicName, labelIds, endTime, cancellationToken);
            }
            else
            {
                _logger.Debug($"Watch renewal check: expires in {timeUntilExpiration:d\\.hh\\:mm\\:ss}, no renewal needed yet.");
                // Reschedule the next check (this shouldn't normally happen with proper scheduling)
                await ScheduleNextRenewalCheckAsync(topicName, labelIds, endTime, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during watch renewal check: {ex.Message}");
            _logger.Debug($"Watch renewal error details: {ex}");
            
            // Schedule retry in 5 minutes on error
            ScheduleRenewalTimer(TimeSpan.FromMinutes(ScheduleWatchRetryMinutes), topicName, labelIds, endTime, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // For synchronous disposal, we can only clean up what we can synchronously
            _renewalTimer?.Dispose();
            _renewalTimer = null;
            _watchBroker?.Dispose();
            _disposed = true;
            _logger.Info("Gmail watch manager disposed (sync cleanup only).");
        }
    }
}
