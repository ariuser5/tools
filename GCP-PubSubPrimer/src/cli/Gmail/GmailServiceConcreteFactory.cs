using DCiuve.Gcp.ExtensionDomain;
using DCiuve.Shared.Logging;
using Google.Apis.Gmail.v1;
using Google.Apis.Http;

namespace DCiuve.Gcp.PubSub.Cli.Gmail;

/// <summary>
/// Gmail-specific concrete factory that creates authenticated Gmail strategies.
/// </summary>
public class GmailServiceConcreteFactory(ILogger logger) : IServiceConcreteFactory
{
	public string ServiceName => Constants.GmailServiceTypeName;

	public string[] NormalizeScopes(string[]? inputScopes)
	{
		// If input scopes are null or empty, return Gmail default scopes
		if (inputScopes == null || inputScopes.Length == 0)
		{
			return [GmailService.Scope.GmailReadonly];
		}

		return [.. inputScopes.Select(x =>
		{
			return x switch
			{
				"gmail-readonly" => GmailService.Scope.GmailReadonly,
				"gmail-modify" => GmailService.Scope.GmailModify,
				"gmail-send" => GmailService.Scope.GmailSend,
				_ => throw new ArgumentException($"Unsupported Gmail scope: {x}", nameof(inputScopes))
			};
		})];
	}

	public GmailServiceStrategy CreateAuthenticatedStrategy(
		IConfigurableHttpClientInitializer gcpClientInitializer,
		string applicationName)
	{
		var authenticatedGmailService = new GcpWatchBrokerService(gcpClientInitializer, applicationName);
		return new GmailServiceStrategy(authenticatedGmailService, logger);
	}

	IServiceStrategy IServiceConcreteFactory.CreateStrategy(
		IConfigurableHttpClientInitializer gcpClientInitializer,
		string applicationName)
	{
		return CreateAuthenticatedStrategy(gcpClientInitializer, applicationName);
	}
}
