namespace DCiuve.Tools.Gcp.PubSub.Cli;

/// <summary>
/// Abstract factory for creating authenticated service strategies.
/// Handles the complete flow: scope normalization → authentication → strategy creation.
/// </summary>
public interface IServiceStrategyAbstractFactory
{
	/// <summary>
	/// Creates an authenticated strategy for the specified service.
	/// </summary>
	/// <param name="serviceName">Name of the service (gmail, drive, calendar, etc.).</param>
	/// <param name="clientSecretStream">Stream containing the client secret for authentication.</param>
	/// <param name="applicationName">Application name for the service.</param>
	/// <param name="customScopes">Custom scopes to use, or null for default scopes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Authenticated service strategy with initialized inner service.</returns>
	Task<IServiceStrategy> CreateStrategyAsync(
		string serviceName,
		Stream clientSecretStream,
		string applicationName,
		string[]? customScopes,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all available service names.
	/// </summary>
	/// <returns>Array of supported service names.</returns>
	string[] GetAvailableServices();
}
