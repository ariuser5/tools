using DCiuve.Gcp.ExtensionDomain.Gmail;
using DCiuve.Gcp.Mailflow.Cli.Commands;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Shared.Logging;
using Google.Apis.Auth.OAuth2;

namespace DCiuve.Gcp.Mailflow.Cli.Subscribe;

public class SubscriptionStrategyFactory(
	ILogger logger,
	IGmailClient gmailClient,
	Lazy<ICredential> pubsubAccessCredential)
{

	public ISubscriptionStrategy CreateStrategy(SubscribeOptions options)
	{
		return options.UsePullSubscription switch
		{
			true => CreatePullStrategy(options),
			false => CreatePollingStrategy(options),
		};
	}

	private PullSubscriptionStrategy CreatePullStrategy(SubscribeOptions options)
	{
		var emailSubscriber = new EmailSubscriber(gmailClient, pubsubAccessCredential.Value);
		return new PullSubscriptionStrategy(logger, options, emailSubscriber, gmailClient);
	}

	private PollingSubscriptionStrategy CreatePollingStrategy(SubscribeOptions options)
	{
		var emailFetcher = new EmailFetcher(gmailClient);
		var emailPoller = new EmailPoller(emailFetcher);
		return new PollingSubscriptionStrategy(logger, emailPoller);
	}
}