namespace DCiuve.Gcp.ExtensionDomain.Authentication;

public interface ICredentialSource
{
	Stream ClientSecretStream { get; }
	string[] Scopes { get; }
	string? CredentialsPath { get; }
	string? User { get; }
}

public class CredentialSource : ICredentialSource
{
	public Stream ClientSecretStream { get; set; } = Stream.Null;
	public string[] Scopes { get; set; } = Array.Empty<string>();
	public string? CredentialsPath { get; set; }
	public string? User { get; set; }
}
