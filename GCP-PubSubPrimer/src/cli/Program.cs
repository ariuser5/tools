using DCiuve.Tools.Gcp.PubSub.Cli;
using DCiuve.Shared.Logging;
using CommandLine;

const string defaultApplicationName = "MyApp-PubSubPrimer";
const string projectIdEnvVar = "GCP_PUBSUB_PROJECTID";
const string topicIdEnvVar = "GCP_PUBSUB_TOPICID";
const string secretPathEnvVar = "GCP_CREDENTIALS_PATH";

// Parse command line arguments with verb support
var result = Parser.Default.ParseArguments<WatchOptions, CancelOptions>(args);
return result.MapResult(
    (WatchOptions opts) => RunWatchCommand(opts),
    (CancelOptions opts) => RunCancelCommand(opts),
    HandleParseError);

static int RunWatchCommand(WatchOptions options)
{
    try
    {
        return RunWatchApplicationAsync(options).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fatal error: {ex.Message}");
        return 1;
    }
}

static int RunCancelCommand(CancelOptions options)
{
    try
    {
        return RunCancelApplicationAsync(options).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fatal error: {ex.Message}");
        return 1;
    }
}

static async Task<int> RunWatchApplicationAsync(WatchOptions options)
{
	// Setup logger
	var logger = new Logger
	{
		Verbosity = (LogLevel)options.Verbose
	};

	// Get the application name from options or use default
	string applicationName = options.ApplicationName ?? defaultApplicationName;
	logger.Debug("Using application name: {0}", applicationName);

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

	// Create secret stream for authentication
	using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);

	// Set up cancellation token for graceful shutdown
	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true; // Prevent immediate exit
		logger.Info("Shutdown requested...");
		cts.Cancel();
	};

	try
	{
		var abstractFactory = new ServiceStrategyAbstractFactory(logger);
		var mediator = new ServiceMediator(abstractFactory, logger);

		// Parse custom scopes if provided
		string[]? customScopes = null;
		if (!string.IsNullOrWhiteSpace(options.Scopes))
		{
			customScopes = options.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.ToArray();
		}

		// Execute watch using the service mediator
		await mediator.ExecuteWatchAsync(
			options.WatchService,
			secretStream,
			applicationName,
			projectId,
			topicId,
			customScopes,
			options.Frequency,
			options.ForceNew,
			cts.Token);

		return 0;
	}
	catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
	{
		logger.Info("Operation cancelled by user.");
		return 0;
	}
	catch (Exception ex)
	{
		logger.Error("Watch execution failed: {0}", ex.Message);
		logger.Debug("Watch execution exception details: {0}", ex);
		return 1;
	}
}

static async Task<int> RunCancelApplicationAsync(CancelOptions options)
{
	// Setup logger
	var logger = new Logger
	{
		Verbosity = (LogLevel)options.Verbose
	};

	// Get the application name from options or use default
	string applicationName = options.ApplicationName ?? defaultApplicationName;
	logger.Debug("Using application name: {0}", applicationName);

	// Get configuration values (command line args override environment variables)
	string secretPath = options.SecretPath
		?? Environment.GetEnvironmentVariable(secretPathEnvVar)
		?? throw new InvalidOperationException($"Secret path not specified via --secret-path or {secretPathEnvVar} env var");

	string? topicId = options.TopicId ?? Environment.GetEnvironmentVariable(topicIdEnvVar);

	logger.Info("Cancelling {0} watch...", options.WatchService.ToUpper());
	if (!string.IsNullOrEmpty(topicId))
	{
		logger.Debug("Target topic: {0}", topicId);
	}
	logger.Debug("Using credentials from: {0}", secretPath);

	// Create secret stream for authentication
	using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);

	// Create cancellation token for Ctrl+C handling
	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true;
		logger.Info("Cancellation requested by user. Stopping...");
		cts.Cancel();
	};

	try
	{
		// Create abstract factory and service mediator
		var abstractFactory = new ServiceStrategyAbstractFactory(logger);
		var mediator = new ServiceMediator(abstractFactory, logger);

		// Cancel watch using the service mediator
		bool cancelled = await mediator.CancelWatchAsync(
			options.WatchService,
			secretStream,
			applicationName,
			cts.Token);
		
		if (cancelled)
		{
			logger.Info("Watch successfully cancelled.");
			return 0;
		}
		else
		{
			logger.Warning("No active watch found to cancel or cancellation failed.");
			return 1;
		}
	}
	catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
	{
		logger.Info("Operation cancelled by user.");
		return 1;
	}
	catch (Exception ex)
	{
		logger.Error("Error during cancellation: {0}", ex.Message);
		logger.Debug("Exception details: {0}", ex);
		return 1;
	}
}

static int HandleParseError(IEnumerable<CommandLine.Error> errors)
{
    Console.WriteLine("Failed to parse command line arguments.");
    return 1;
}

