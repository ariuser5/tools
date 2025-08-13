namespace DCiuve.Tools.Gcp.PubSub.Cli;

/// <summary>
/// Base interface for watch results from different services.
/// </summary>
public interface IWatchResult
{
	bool IsNewlyCreated { get; }
	DateTime CreatedAt { get; }
	DateTime? Expiration { get; }
	object Response { get; } // Service-specific response
}
