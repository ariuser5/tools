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
		var credentialsFilePath = Environment.GetEnvironmentVariable(credentialsEnvVar)
			?? throw new InvalidOperationException($"{credentialsEnvVar} environment variable is not set.");

		UserCredential credential;
		using var stream = new FileStream(credentialsFilePath, FileMode.Open, FileAccess.Read);
		string credPath = "token.json";
		credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
			clientSecrets: GoogleClientSecrets.FromStream(stream).Secrets,
			scopes: [GmailService.Scope.GmailReadonly],
			user: "user",
			taskCancellationToken: CancellationToken.None,
			dataStore: new FileDataStore(credPath, true));

		return credential;
	}
}
