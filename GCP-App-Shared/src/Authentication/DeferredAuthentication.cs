using DCiuve.Gcp.Auth;
using DCiuve.Gcp.ExtensionDomain.Authentication;
using DCiuve.Shared.Logging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;

namespace DCiuve.Gcp.App.Shared.Authentication;

public class DeferredAuthentication : IConfigurableHttpClientInitializer
{
	private readonly ICredentialSource _credentialSource;
	private readonly CancellationToken _cancellationToken;

	private UserCredential? _credential = null;

	public DeferredAuthentication(
		ICredentialSource credentialSource,
		CancellationToken cancellationToken)
	{
		_credentialSource = credentialSource;
		_cancellationToken = cancellationToken;
	}

	public ILogger? Logger { get; set; } = null;


	public async Task<UserCredential> GetCredential()
	{
		if (_credential != null) return _credential;
		
		Logger?.Debug("Begin Google Authentication...");
		Logger?.Debug("Using scopes {1}", string.Join(", ", _credentialSource.Scopes));

		_credential = await AuthenticateAsync(_cancellationToken);
		
		Logger?.Debug("Authentication successful.");

		return _credential;
	}
	
	public bool IsExecuted()
	{
		return _credential != null;
	}
	
	void IConfigurableHttpClientInitializer.Initialize(ConfigurableHttpClient httpClient)
	{
		var credential = GetCredential().GetAwaiter().GetResult();
		credential.Initialize(httpClient);
	}
	
	private Task<UserCredential> AuthenticateAsync(CancellationToken cancellationToken)
	{
		if (_credentialSource.User != null && _credentialSource.DataStore != null)
		{
			return Authenticator.Authenticate(
				_credentialSource.ClientSecrets,
				_credentialSource.Scopes,
				_credentialSource.User,
				_credentialSource.DataStore,
				cancellationToken);
		}

		if (_credentialSource.User != null)
		{
			return Authenticator.Authenticate(
				_credentialSource.ClientSecrets,
				_credentialSource.Scopes,
				_credentialSource.User,
				dataStore: null,
				cancellationToken: cancellationToken);
		}

		if (_credentialSource.DataStore != null)
		{
			return Authenticator.Authenticate(
				_credentialSource.ClientSecrets,
				_credentialSource.Scopes,
				dataStore: _credentialSource.DataStore,
				cancellationToken: cancellationToken);
		}

		// Fallback to the minimal overload
		return Authenticator.Authenticate(
			_credentialSource.ClientSecrets,
			_credentialSource.Scopes,
			dataStore: null,
			cancellationToken: cancellationToken);
	}
}
