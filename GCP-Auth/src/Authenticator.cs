using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Util.Store;
using System.Text;

namespace DCiuve.Tools.Gcp.Auth;

public class Authenticator
{
	public static async Task<UserCredential> Authorize(string secretJson)
	{
		var bytes = Encoding.UTF8.GetBytes(secretJson);
		using var stream = new MemoryStream(bytes);
		return await Authorize(stream);
	}
	
	public static async Task<UserCredential> Authorize(Stream secretStream)
	{
		UserCredential credential;
		string credPath = "token.json";
		credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
			clientSecrets: GoogleClientSecrets.FromStream(secretStream).Secrets,
			scopes: new[] { GmailService.Scope.GmailReadonly },
			user: "user",
			taskCancellationToken: CancellationToken.None,
			dataStore: new FileDataStore(credPath, true));

		return credential;
	}
}
