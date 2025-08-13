using Google.Apis.Http;

namespace DCiuve.Tools.Gcp.PubSub.Cli;

/// <summary>
/// Service-specific factory for creating strategies with authenticated inner services.
/// Each service (Gmail, Drive, Calendar) will have its own concrete implementation.
/// </summary>
public interface IServiceConcreteFactory
{
	/// <summary>
	/// Gets the service name this factory handles.
	/// </summary>
	string ServiceName { get; }

	/// <summary>
	/// Normalizes scopes for this specific service.
	/// </summary>
	/// <param name="inputScopes">Input scopes or null for defaults.</param>
	/// <returns>Normalized scopes for this service.</returns>
	string[] NormalizeScopes(string[]? inputScopes);

	/// <summary>
	/// Creates a strategy instance with an authenticated inner service.
	/// </summary>
	/// <param name="gcpClientInitializer">Http client initializer for GCP access.</param>
	/// <param name="applicationName">Application name.</param>
	/// <returns>Strategy with initialized inner service.</returns>
	IServiceStrategy CreateStrategy(
		IConfigurableHttpClientInitializer gcpClientInitializer,
		string applicationName);
}
