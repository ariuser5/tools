using DCiuve.Gcp.ExtensionDomain.Gmail;
using DCiuve.Gcp.Mailflow.Cli.Commands;
using DCiuve.Gcp.Mailflow.Cli.Services;
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.Mailflow.Cli.Subscribe;

public class PullSubscriptionStrategy(
	ILogger logger,
	SubscribeOptions options,
	EmailSubscriber emailSubscriber,
	IGmailClient gmailClient
) : ISubscriptionStrategy
{

	public Func<IReadOnlyCollection<EmailMessage>, CancellationToken, Task> OutputAction { get; set; }
		= (_, __) => throw new InvalidOperationException("OutputAction must be set before executing the strategy.");

	public async Task<int> ExecuteAsync(EmailSubscription subscription, CancellationToken cancellationToken)
	{
	logger.Info("Starting pull subscription-based email monitoring...");
	logger.Info($"Subscription ID: {subscription.Name}");

		if (!options.SetupWatch)
		{
			await StartEmailMonitoringAsync(subscription, cancellationToken);
			return 0;
		}

		// Create a cancellation token source for coordinated cancellation
		using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		// Start watch management in background
		var watchTask = StartWatchManagementAsync(
			topicName: subscription.TopicName,
			labelIds: [.. subscription.Filter.LabelIds],
			endTime: subscription.EndTime,
			cancellationToken: watchCts.Token);

		try
		{
			// Start email monitoring as the master task
			await StartEmailMonitoringAsync(subscription, cancellationToken);
			return 0;
		}
		catch (Exception ex)
		{
			if (ex is OperationCanceledException oce && oce.CancellationToken.IsCancellationRequested)
			{
				// Cancellation was requested, likely due to user action or duration ending
			}
			else
			{
				logger.Error("Error during email monitoring: {0}", ex.Message);
				logger.Debug("Exception details: {0}", ex);
				logger.Info("Stopping watch management due to monitoring failure...");
			}
			
			watchCts.Cancel();
			throw;
		}
		finally
		{
			// Ensure watch management is stopped and we wait for cleanup
			watchCts.Cancel();
			try
			{
				await watchTask;
			}
			catch (OperationCanceledException) { /* Expected */ }
			catch (Exception ex)
			{
				logger.Warning($"Error during watch management cleanup: {ex.Message}");
			}
		}
    }

	/// <summary>
	/// Starts email monitoring using pull subscription via EmailSubscriber.
	/// This method sets up real pull subscription handling instead of polling.
	/// </summary>
	/// <param name="subscription">The subscription configuration.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task StartEmailMonitoringAsync(
		EmailSubscription subscription,
		CancellationToken cancellationToken)
	{
	logger.Info("Starting email monitoring (pull subscription mode)...");

		try
		{
			// Start the pull subscription listener
			var (projectId, topicId) = DecomposeTopicName(subscription.TopicName);

			var messageStream = emailSubscriber.StartPullSubscriptionListenerAsync(
				projectId, subscription.Name, subscription.Filter, cancellationToken);

			logger.Info("Pull subscription listener started successfully");

			// Process messages from the pull subscription stream
			await foreach (var message in messageStream.WithCancellation(cancellationToken))
			{
				logger.Debug("Inbound notification received at {0} - batchId '{1}'.", DateTime.UtcNow, message.BatchId);
				await HandleInboundMessage(message, cancellationToken);
			}
		}
		finally
		{
			// Stop the pull subscription listener
			try
			{
				emailSubscriber.Stop();
				logger.Info("Pull subscription listener stopped.");
			}
			catch (Exception ex)
			{
				logger.Warning($"Error stopping pull subscription listener: {ex.Message}");
			}
		}
	}

	private async Task HandleInboundMessage(InboundMessage message, CancellationToken cancellationToken)
	{
		try
		{
			var processedMessage = await message.DetailsProcessing;
			if (processedMessage.IsFiltered)
			{
				logger.Debug("Message filtered out by subscription filter.");
				return;
			}

			logger.Info($"Received new email!");
			await OutputAction([processedMessage.EmailMessage], cancellationToken);
		}
		catch (Exception ex)
		{
			logger.Error("Error processing inbound message: {0}", ex.Message);
			logger.Debug("Exception details: {0}", ex);
		}
	}

	/// <summary>
	/// Starts Gmail watch management in the background.
	/// </summary>
	/// <param name="topicName">The name of the Pub/Sub topic.</param>
	/// <param name="endTime">When to stop managing watches (null for indefinite).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task StartWatchManagementAsync(
		string topicName,
		string[] labelIds,
		DateTime? endTime,
		CancellationToken cancellationToken)
	{
		GmailWatchManager? watchManager = null;
		try
		{
			var watchAppName = options.ApplicationName ?? AppDomain.CurrentDomain.FriendlyName;
			watchManager = new GmailWatchManager(gmailClient, logger, watchAppName);
			await watchManager.StartWatchManagementAsync(
				topicName: topicName,
				labelIds: labelIds,
				endTime: endTime,
				cancellationToken: cancellationToken);
		}
		catch (Exception ex)
		{
			logger.Error($"Gmail watch management failed: {ex.Message}");
			logger.Debug("Exception details: {0}", ex);
			throw;
		}
		finally
		{
			if (watchManager != null)
			{
				await watchManager.StopWatchManagementAsync();
				watchManager.Dispose();
			}
		}
	}

    /// <summary>
    /// Decomposes a Pub/Sub topic name into its project ID and topic ID components.
    /// </summary> <summary>
    private static (string projectId, string topicId) DecomposeTopicName(string topicName)
    {
        if (string.IsNullOrEmpty(topicName))
            throw new ArgumentException("Topic name cannot be null or empty.", nameof(topicName));

        var parts = topicName.Split('/');
        if (parts.Length < 3 || parts[0] != "projects" || parts[2] != "topics")
            throw new ArgumentException("Invalid topic name format. Expected format: projects/{projectId}/topics/{topicId}", nameof(topicName));

        var projectId = parts[1];
        var topicId = parts[3];

        return (projectId, topicId);
    }
}