using DCiuve.Gcp.ExtensionDomain.Authentication;

namespace DCiuve.Gcp.Shared.Authentication;

public class CredentialSourceBuilder
{
	private readonly CredentialSource _credentialSource = new();

	public CredentialSourceBuilder WithClientSecretStream(Stream clientSecretStream)
	{
		_credentialSource.ClientSecretStream = clientSecretStream;
		return this;
	}

	public CredentialSourceBuilder WithScopes(params string[] scopes)
	{
		_credentialSource.Scopes = scopes;
		return this;
	}

	public CredentialSourceBuilder WithCredentialsPath(string credentialsPath)
	{
		_credentialSource.CredentialsPath = credentialsPath;
		return this;
	}

	public CredentialSourceBuilder WithUser(string user)
	{
		_credentialSource.User = user;
		return this;
	}

	public ICredentialSource Build() => _credentialSource;
}