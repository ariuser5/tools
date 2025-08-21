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
		return options.UsePushNotifications switch
		{
			true => CreatePushStrategy(options),
			false => CreatePollingStrategy(options),
		};
	}

	private PushSubscriptionStrategy CreatePushStrategy(SubscribeOptions options)
	{
		var emailSubscriber = new EmailSubscriber(gmailClient, pubsubAccessCredential.Value);
		return new PushSubscriptionStrategy(logger, options, emailSubscriber, gmailClient);
	}

	private PollingSubscriptionStrategy CreatePollingStrategy(SubscribeOptions options)
	{
		var emailFetcher = new EmailFetcher(gmailClient);
		var emailPoller = new EmailPoller(emailFetcher);
		return new PollingSubscriptionStrategy(logger, emailPoller);
	}
}