using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;

namespace DCiuve.Tools.Gcp.PubSub;

public class GmailWatchService : IDisposable
{
    private readonly GmailService _service;
    private bool _disposed;

    public GmailWatchService(UserCredential authorization, string applicationName)
    {
        if (authorization == null)
            throw new ArgumentNullException(nameof(authorization));
        if (string.IsNullOrEmpty(applicationName))
            throw new ArgumentException("Application name cannot be null or empty", nameof(applicationName));

        _service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = authorization,
            ApplicationName = applicationName,
        });
    }

    public async Task<string> WatchInboxAsync(string projectId, string topicId)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("Project ID cannot be null or empty", nameof(projectId));
        
        if (string.IsNullOrEmpty(topicId))
            throw new ArgumentException("Topic ID cannot be null or empty", nameof(topicId));

        var watchRequest = new WatchRequest
        {
            LabelFilterAction = "include",
            LabelIds = new[] { "INBOX" },
            TopicName = $"projects/{projectId}/topics/{topicId}"
        };

        var watchResponse = await _service.Users.Watch(watchRequest, "me").ExecuteAsync();
        
        // Return the history ID for potential use by caller
        return watchResponse.HistoryId?.ToString() ?? "N/A";
    }

    public async Task<bool> StopWatchAsync()
    {
        try
        {
            await _service.Users.Stop("me").ExecuteAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _service?.Dispose();
        }
        _disposed = true;
    }
}
