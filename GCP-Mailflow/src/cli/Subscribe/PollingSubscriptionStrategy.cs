using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.Mailflow.Cli.Subscribe;

public class PollingSubscriptionStrategy(
	ILogger logger,
	EmailPoller emailPoller
) : ISubscriptionStrategy
{
	public Func<IReadOnlyCollection<EmailMessage>, CancellationToken, Task> OutputAction { get; set; }
		= (emails, ct) => throw new InvalidOperationException("OutputAction must be set before executing the strategy.");

    public async Task<int> ExecuteAsync(EmailSubscription subscription, CancellationToken cancellationToken)
    {
        logger.Info("Starting polling-based email monitoring...");
        logger.Info($"Polling interval: {subscription.PollingIntervalSeconds} seconds");

        var messageStream = emailPoller.StartPollingAsync(subscription, cancellationToken);

		await foreach (var emails in messageStream.WithCancellation(cancellationToken))
		{
			if (emails.Count > 0)
			{
				logger.Info($"Received {emails.Count} new emails!");
				await OutputAction(emails, cancellationToken);
			}
		}
		
        return 0;
    }
}