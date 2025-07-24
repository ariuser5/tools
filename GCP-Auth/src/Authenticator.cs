using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using System.Text;

namespace DCiuve.Tools.Gcp.Auth;

public class Authenticator
{
	public static async Task<UserCredential> Authenticate(
		string secretJson,
		IEnumerable<string> scopes,
		string user = "user",
		string credentialsPath = "token.json",
		CancellationToken cancellationToken = default)
	{
		var bytes = Encoding.UTF8.GetBytes(secretJson);
		using var stream = new MemoryStream(bytes);
		return await Authenticate(stream, scopes, user, credentialsPath, cancellationToken);
	}
	
	public static async Task<UserCredential> Authenticate(
		Stream secretStream,
		IEnumerable<string> scopes,
		string user = "user",
		string credentialsPath = "token.json",
		CancellationToken cancellationToken = default)
	{
		UserCredential credential;
		credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
			clientSecrets: GoogleClientSecrets.FromStream(secretStream).Secrets,
			scopes: scopes,
			user: user,
			taskCancellationToken: cancellationToken,
			dataStore: new FileDataStore(credentialsPath, true));

		return credential;
	}
}
