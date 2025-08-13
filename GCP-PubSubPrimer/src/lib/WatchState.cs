namespace DCiuve.Tools.Gcp.PubSub;

/// <summary>
/// Generic watch state for any Google service.
/// </summary>
public class WatchState
{
    public string ServiceType { get; set; } = string.Empty;
    public string WatchId { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> ServiceSpecificData { get; set; } = new();
}
