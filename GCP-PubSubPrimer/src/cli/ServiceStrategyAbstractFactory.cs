using DCiuve.Gcp.App.Shared.Authentication;
using DCiuve.Gcp.ExtensionDomain;
using DCiuve.Gcp.PubSub.Cli.Gmail;
using DCiuve.Gcp.Shared.Authentication;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.PubSub.Cli;

/// <summary>
/// Main abstract factory that orchestrates the complete flow:
/// scope normalization → deferred authentication setup → authenticated strategy creation.
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
		
		var normalizedScopes = concreteFactory.NormalizeScopes(customScopes);
		var credentialSource = new CredentialSourceBuilder()
			.WithClientSecretStream(clientSecretStream)
			.WithScopes(normalizedScopes)
			.Build();
		
		var gcpClientInitializer = new DeferredAuthentication(credentialSource, cancellationToken)
		{
			Logger = _logger
		};
		
		var strategy = concreteFactory.CreateStrategy(gcpClientInitializer, applicationName);

		_logger.Debug("Strategy '{0}' created successfully.", serviceName);
		return Task.FromResult(strategy);
	}

	public string[] GetAvailableServices()
	{
		return [.. _concreteFactories.Keys];
	}
}