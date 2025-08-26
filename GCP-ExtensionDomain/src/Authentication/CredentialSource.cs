using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace DCiuve.Gcp.ExtensionDomain.Authentication;

public interface ICredentialSource
{
	ClientSecrets ClientSecrets { get; }
	string[] Scopes { get; }
	IDataStore? DataStore { get; }
	string? User { get; }
}

public class CredentialSource : ICredentialSource
{
	public ClientSecrets ClientSecrets { get; set; } = new ClientSecrets();
	public string[] Scopes { get; set; } = Array.Empty<string>();
	public IDataStore? DataStore { get; set; }
	public string? User { get; set; }
}
