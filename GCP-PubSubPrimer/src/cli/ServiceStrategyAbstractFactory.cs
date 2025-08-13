using DCiuve.Tools.Gcp.Auth;
using DCiuve.Tools.Gcp.ExtensionDomain;
using DCiuve.Tools.Gcp.PubSub.Cli.Gmail;
using DCiuve.Tools.Logging;
using Google.Apis.Http;

namespace DCiuve.Tools.Gcp.PubSub.Cli;

/// <summary>
/// Main abstract factory that orchestrates the complete flow:
/// scope normalization → authentication → authenticated strategy creation.
/// </summary>
public class ServiceStrategyAbstractFactory : IServiceStrategyAbstractFactory
{
	private readonly ILogger _logger;
	private readonly Dictionary<string, IServiceConcreteFactory> _concreteFactories;

	public ServiceStrategyAbstractFactory(ILogger logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_concreteFactories = new Dictionary<string, IServiceConcreteFactory>(StringComparer.OrdinalIgnoreCase)
		{
			[Constants.GmailServiceTypeName] = new GmailServiceConcreteFactory(_logger),
		};
	}

	public Task<IServiceStrategy> CreateStrategyAsync(
		string serviceName,
		Stream clientSecretStream,
		string applicationName,
		string[]? customScopes,
		CancellationToken cancellationToken = default)
	{
		if (!_concreteFactories.TryGetValue(serviceName, out var concreteFactory))
			throw new ArgumentException($"Unsupported service: {serviceName}", nameof(serviceName));

		_logger.Debug("Creating {0} strategy...", serviceName);

		var strategy = concreteFactory.CreateStrategy(
			gcpClientInitializer: new DeferredAuthentication(
				_logger,
				concreteFactory,
				clientSecretStream,
				customScopes,
				cancellationToken),
			applicationName);

		_logger.Debug("Strategy '{0}' created successfully.", serviceName);
		return Task.FromResult(strategy);
	}

	public string[] GetAvailableServices()
	{
		return [.. _concreteFactories.Keys];
	}
}

class DeferredAuthentication(
	ILogger logger,
	IServiceConcreteFactory concreteFactory,
	Stream clientSecretStream,
	string[]? customScopes,
	CancellationToken cancellationToken
) : IConfigurableHttpClientInitializer
{
	public void Initialize(ConfigurableHttpClient httpClient)
	{
		logger.Debug("Begin Google Authentication...");

		var normalizedScopes = concreteFactory.NormalizeScopes(customScopes);
		logger.Debug("Using scopes for {0}: {1}", concreteFactory.ServiceName, string.Join(", ", normalizedScopes));

		logger.Debug("Authenticating with Google credentials...");
		var authentication = Authenticator.Authenticate(
			clientSecretStream,
			normalizedScopes,
			cancellationToken: cancellationToken
		).GetAwaiter().GetResult();

		cancellationToken.ThrowIfCancellationRequested();
			
		logger.Debug("Authentication successful.");

		authentication.Initialize(httpClient);
	}
}