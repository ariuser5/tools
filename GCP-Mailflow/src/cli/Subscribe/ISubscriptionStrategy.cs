using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Cli.Subscribe;

public interface ISubscriptionStrategy
{
	/// <summary>
	/// Executes the subscription logic based on the provided subscription configuration.
	/// </summary>
	/// <param name="subscription">The email subscription configuration.</param>
	/// <param name="options">The original command options (for output formatting, etc.).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation, with an integer result code.</returns>
	Task<int> ExecuteAsync(EmailSubscription subscription, CancellationToken cancellationToken);

	/// <summary>
	/// Action to perform when new email messages are received.
	/// </summary>
	public Func<IReadOnlyCollection<EmailMessage>, CancellationToken, Task> OutputAction { get; set; }
}