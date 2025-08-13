using DCiuve.Tools.Gcp.ExtensionDomain;
using DCiuve.Tools.Logging;
using Google.Apis.Gmail.v1.Data;

namespace DCiuve.Tools.Gcp.PubSub.Cli.Gmail;

/// <summary>
/// Gmail-specific implementation of the service strategy.
/// Now accepts an authenticated service to avoid recreating it for each operation.
/// </summary>
public class GmailServiceStrategy(
	GcpWatchBrokerService authenticatedGmailService,
	ILogger logger
) : IServiceStrategy
{
	public async Task<IWatchResult> ExecuteWatchAsync(
		string projectId,
		string topicId,
		bool forceNew,
		CancellationToken cancellationToken = default)
	{
		logger.Debug("Creating Gmail watch request...");
		var topicName = $"projects/{projectId}/topics/{topicId}";
		var watchResult = await authenticatedGmailService.WatchGmailAsync(topicName, forceNew: forceNew, cancellationToken: cancellationToken);

		if (watchResult.IsNewlyCreated)
			logger.Info("New {0} watch created successfully", Constants.GmailServiceTypeName);
		else
		{
			var age = DateTime.UtcNow - watchResult.CreatedAt;
			logger.Info("Using existing {0} watch (age: {1:d\\.hh\\:mm\\:ss})",
				Constants.GmailServiceTypeName, age);
		}
		
		return new GmailWatchResult(watchResult);
	}

	public async Task<bool> StopWatchAsync(CancellationToken cancellationToken = default)
	{
		logger.Debug("Cancelling Gmail watch...");
		logger.Warning("Gmail API limitation: This will cancel ALL active Gmail watches, not just the one for the specified topic");

		var result = await authenticatedGmailService.StopGmailWatchAsync(cancellationToken);
		
		if (result)
			logger.Info("Gmail watch successfully cancelled.");
		else
			logger.Warning("No active Gmail watch found to cancel or cancellation failed.");

		return result;
	}
}

class GmailWatchResult(WatchResult<WatchResponse> gmailResult) : IWatchResult
{
	public bool IsNewlyCreated => gmailResult.IsNewlyCreated;
	public DateTime CreatedAt => gmailResult.CreatedAt;
	public DateTime? Expiration => gmailResult.Expiration;
	public object Response => gmailResult.Response;
	
	public WatchResult<WatchResponse> OriginalResult => gmailResult;
}
