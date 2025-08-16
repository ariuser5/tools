namespace DCiuve.Gcp.PubSub.Cli;

/// <summary>
/// Strategy interface for different Google service operations.
/// Each service (Gmail, Drive, Calendar) implements this interface.
/// </summary>
public interface IServiceStrategy
{
	/// <summary>
	/// Executes a watch request for this service.
	/// </summary>
	/// <param name="projectId">GCP Project ID.</param>
	/// <param name="topicId">PubSub Topic ID.</param>
	/// <param name="forceNew">Whether to force creation of a new watch.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The watch result.</returns>
	Task<IWatchResult> ExecuteWatchAsync(
		string projectId,
		string topicId,
		bool forceNew,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Cancels active watch requests for this service.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if cancellation was successful.</returns>
	Task<bool> StopWatchAsync(CancellationToken cancellationToken = default);
}
