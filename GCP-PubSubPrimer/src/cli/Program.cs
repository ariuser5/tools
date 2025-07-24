using Google.Apis.Auth.OAuth2;
using DCiuve.Tools.Gcp.Auth;
using DCiuve.Tools.Gcp.PubSub.Cli;
using DCiuve.Tools.Gcp.PubSub;
using CommandLine;
using System.Reflection;
using Google.Apis.Gmail.v1;

const string applicationName = "PubSubGmailPrimer";
const string projectIdEnvVar = "GCP_PUBSUB_PROJECTID";
const string topicIdEnvVar = "GCP_PUBSUB_TOPICID";
const string secretPathEnvVar = "GCP_CREDENTIALS_PATH";

// Parse command line arguments
var result = Parser.Default.ParseArguments<Options>(args);
return result.MapResult(RunApplication, HandleParseError);

static int RunApplication(Options options)
{
    try
    {
        return RunApplicationAsync(options).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fatal error: {ex.Message}");
        return 1;
    }
}

static async Task<int> RunApplicationAsync(Options options)
{
	// Setup logger
	var logger = new Logger
	{
		Verbosity = (LogLevel)options.Verbose
	};

	// Get configuration values (command line args override environment variables)
	string projectId = options.ProjectId
		?? Environment.GetEnvironmentVariable(projectIdEnvVar)
		?? throw new InvalidOperationException($"Project ID not specified via --project-id or {projectIdEnvVar} env var");

	string topicId = options.TopicId
		?? Environment.GetEnvironmentVariable(topicIdEnvVar)
		?? throw new InvalidOperationException($"Topic ID not specified via --topic-id or {topicIdEnvVar} env var");

	string secretPath = options.SecretPath
		?? Environment.GetEnvironmentVariable(secretPathEnvVar)
		?? throw new InvalidOperationException($"Secret path not specified via --secret-path or {secretPathEnvVar} env var");

	logger.Info("Starting {0} API Watch with Project: {1}, Topic: {2}", options.WatchService.ToUpper(), projectId, topicId);
	logger.Debug("Using credentials from: {0}", secretPath);

	// Get the watch action and its default scopes
	var watchAction = GetWatchAction(options.WatchService);
	var scopesInput = (options.Scopes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
		.Select(s => s.Trim())
		.Where(s => !string.IsNullOrWhiteSpace(s))
		.ToArray();
	var scopes = watchAction.MapScopes(scopesInput);

	logger.Debug("Using scopes: {0}", string.Join(", ", scopes));

	// Load the service account credentials
	using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);

	logger.Debug("Authorizing with Google credentials...");
	UserCredential authorization = await Authenticator.Authenticate(secretStream, scopes);
	logger.Debug("Authorization successful.");

	// Handle frequency options
	if (options.Frequency.HasValue)
	{
		logger.Info("Running with repeat frequency: {0} minutes", options.Frequency.Value);

		// Schedule the task to run at specified frequency
		var timer = new Timer(
			callback: async _ => await watchAction.Execute(authorization, applicationName, projectId, topicId, logger),
			state: null,
			dueTime: TimeSpan.Zero,
			period: TimeSpan.FromMinutes(options.Frequency.Value));

		// Keep the application running
		logger.Info("Press [CTRL+C] to exit...");
		Console.ReadLine();
	}
	else
	{
		logger.Info("Running once...");
		await watchAction.Execute(authorization, applicationName, projectId, topicId, logger);
	}

	return 0;
}

static IWatchAction GetWatchAction(string serviceName)
{
	var watchActions = Assembly.GetExecutingAssembly()
		.GetTypes()
		.Where(t => typeof(IWatchAction).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
		.Select(t => (IWatchAction?)Activator.CreateInstance(t)
			?? throw new InvalidOperationException($"Could not create instance of watch action {t.FullName}"))
		.ToList();

	return watchActions.Single(a => a.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
}

static int HandleParseError(IEnumerable<CommandLine.Error> errors)
{
    Console.WriteLine("Failed to parse command line arguments.");
    return 1;
}

public interface IWatchAction
{
	/// <summary>
	/// Gets the name of the watch action.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Maps scope aliases to actual scopes. If no aliases are provided, returns default scopes.
	/// </summary>
	/// <param name="scopeAliases">Optional scope aliases to map.</param>
	/// <returns>Array of scopes.</returns>
	string[] MapScopes(IEnumerable<string>? scopeAliases);
	
	/// <summary>
	/// Executes the watch action.
	/// </summary>
	/// <param name="authorization">Authorized user credential.</param>
	/// <param name="applicationName">Application name for the API client.</param>
	/// <param name="projectId">GCP Project ID.</param>
	/// <param name="topicId">PubSub Topic ID.</param>
	/// <param name="logger">Logger instance for output.</param>
    Task Execute(
		UserCredential authorization,
		string applicationName,
		string projectId,
		string topicId,
		Logger logger);
}

// Gmail watch action implementation
public class GmailWatchAction : IWatchAction
{
    public static string[] DefaultScopes => [GmailService.Scope.GmailReadonly];

	public string Name => "gmail";

	public string[] MapScopes(IEnumerable<string>? scopeAliases)
	{
		if (scopeAliases == null || !scopeAliases.Any())
			return DefaultScopes;
		
		throw new NotImplementedException(
			"Not implemented for custom scope aliases. Only default scopes are supported.");
	}

	public async Task Execute(
		UserCredential authorization,
		string applicationName,
		string projectId,
		string topicId,
		Logger logger)
	{
		try
		{
			logger.Debug("Creating Gmail watch service...");
			using var gmailService = new GmailWatchService(authorization, applicationName);

			logger.Debug("Creating watch request for inbox...");
			var historyId = await gmailService.WatchInboxAsync(projectId, topicId);

			logger.Info("Watch request successful: {0}", historyId);
		}
		catch (Exception ex)
		{
			logger.Error("An error occurred during watch setup: {0}", ex.Message);
			logger.Debug("Exception details: {0}", ex);
		}
	}
}