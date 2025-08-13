using System.Text.Json;

namespace DCiuve.Tools.Gcp.PubSub;

/// <summary>
/// Manages watch state persistence for any Google service.
/// </summary>
public class WatchStateManager
{
    const string WatchesDirectory = "GcpPubSubWatches";
    const string StateFileNameTemplate = "{0}-watch-{1}.json"; // {serviceType}-watch-{applicationName}.json
    const int MinutesExpirationBuffer = 5;
    
    private readonly string _applicationName;
    private readonly string _stateDirectory;

    public WatchStateManager(string applicationName, string? stateDirectory = null)
    {
        _applicationName = applicationName;
        _stateDirectory = stateDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            WatchesDirectory);
        
        Directory.CreateDirectory(_stateDirectory);
    }

    private string GetStateFilePath(string serviceType)
    {
        return Path.Combine(
            _stateDirectory,
            string.Format(StateFileNameTemplate, 
                serviceType.ToLowerInvariant(), 
                _applicationName.ToLowerInvariant()));
    }

    /// <summary>
    /// Checks if there's an active watch for the specified service type.
    /// </summary>
    public async Task<bool> IsWatchActiveAsync(string serviceType)
    {
        try
        {
            var state = await LoadWatchStateAsync(serviceType);
            return state != null && state.Expiration > DateTime.UtcNow.AddMinutes(MinutesExpirationBuffer);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current watch state if active for the specified service type.
    /// </summary>
    public async Task<WatchState?> GetActiveWatchStateAsync(string serviceType)
    {
        try
        {
            var state = await LoadWatchStateAsync(serviceType);
            return state != null && state.Expiration > DateTime.UtcNow.AddMinutes(MinutesExpirationBuffer)
                ? state
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves watch state for the specified service type.
    /// </summary>
    public async Task SaveWatchStateAsync(
        string serviceType,
        string watchId,
        DateTime expiration,
        string topicName,
        Dictionary<string, object>? serviceSpecificData = null)
    {
        var state = new WatchState
        {
            ServiceType = serviceType,
            WatchId = watchId,
            TopicName = topicName,
            ApplicationName = _applicationName,
            Expiration = expiration,
            CreatedAt = DateTime.UtcNow,
            ServiceSpecificData = serviceSpecificData ?? new()
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(GetStateFilePath(serviceType), json);
    }

    /// <summary>
    /// Clears watch state for the specified service type.
    /// </summary>
    public Task ClearWatchStateAsync(string serviceType)
    {
        try
        {
            var filePath = GetStateFilePath(serviceType);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore errors when clearing state
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads watch state from file for the specified service type.
    /// </summary>
    private async Task<WatchState?> LoadWatchStateAsync(string serviceType)
    {
        try
        {
            var filePath = GetStateFilePath(serviceType);
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<WatchState>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all active watches across all service types.
    /// </summary>
    public async Task<Dictionary<string, WatchState>> GetAllActiveWatchesAsync()
    {
        var result = new Dictionary<string, WatchState>();
        
        try
        {
            var files = Directory.GetFiles(_stateDirectory, "*-watch-*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var state = JsonSerializer.Deserialize<WatchState>(json);
                    
                    if (state != null && state.Expiration > DateTime.UtcNow.AddMinutes(MinutesExpirationBuffer))
                    {
                        result[state.ServiceType] = state;
                    }
                }
                catch
                {
                    // Skip invalid state files
                }
            }
        }
        catch
        {
            // Return empty if directory issues
        }

        return result;
    }
}