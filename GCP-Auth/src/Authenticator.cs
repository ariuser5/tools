using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using System.Text;

namespace DCiuve.Gcp.Auth;

public class Authenticator
{
	const string DefaultUser = "user";
	const string DefaultCredentialsPath = "token";

	public static async Task<UserCredential> Authenticate(
		string secretJson,
		IEnumerable<string> scopes,
		string user = DefaultUser,
		string credentialsPath = DefaultCredentialsPath,
		CancellationToken cancellationToken = default)
	{
		var bytes = Encoding.UTF8.GetBytes(secretJson);
		using var stream = new MemoryStream(bytes);
		return await Authenticate(stream, scopes, user, credentialsPath, cancellationToken);
	}

	public static async Task<UserCredential> Authenticate(
		Stream secretStream,
		IEnumerable<string> scopes,
		string user = DefaultUser,
		string credentialsPath = DefaultCredentialsPath,
		CancellationToken cancellationToken = default)
	{
		var googleSecrets = GoogleClientSecrets.FromStream(secretStream);
		return await Authenticate(googleSecrets.Secrets, scopes, user, credentialsPath, cancellationToken);
	}
	
	public static async Task<UserCredential> Authenticate(
		ClientSecrets secrets,
		IEnumerable<string> scopes,
		string user = DefaultUser,
		string credentialsPath = DefaultCredentialsPath,
		CancellationToken cancellationToken = default)
	{
		UserCredential credential;
		credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
			clientSecrets: secrets,
			scopes: scopes,
			user: user,
			taskCancellationToken: cancellationToken,
			dataStore: new FileDataStore(credentialsPath, true)
		).ConfigureAwait(false);

		return credential;
	}
}
