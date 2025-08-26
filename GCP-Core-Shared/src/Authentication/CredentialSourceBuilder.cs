using DCiuve.Gcp.ExtensionDomain.Authentication;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace DCiuve.Gcp.Shared.Authentication;

public class CredentialSourceBuilder
{
	private readonly CredentialSource _credentialSource = new();

	public CredentialSourceBuilder WithClientSecretStream(Stream clientSecretStream)
	{
		var clientSecrets = GoogleClientSecrets.FromStream(clientSecretStream).Secrets;
		_credentialSource.ClientSecrets = clientSecrets;
		return this;
	}
	
	public CredentialSourceBuilder WithClientSecret(ClientSecrets clientSecrets)
	{
		_credentialSource.ClientSecrets = clientSecrets;
		return this;
	}

	public CredentialSourceBuilder WithScopes(params string[] scopes)
	{
		_credentialSource.Scopes = scopes;
		return this;
	}

	public CredentialSourceBuilder WithCredentialsPath(string credentialsPath)
	{
		var dataStore = new FileDataStore(credentialsPath);
		_credentialSource.DataStore = dataStore;
		return this;
	}

	public CredentialSourceBuilder WithUser(string user)
	{
		_credentialSource.User = user;
		return this;
	}

	public ICredentialSource Build() => _credentialSource;
}