using CommandLine;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Gcp.Mailflow.Cli.Commands;
using DCiuve.Gcp.Auth;
using DCiuve.Shared.Cli;
using DCiuve.Shared.Logging;

var result = Parser.Default.ParseArguments<FetchOptions, SubscribeOptions>(args);
return result.MapResult(
    (FetchOptions o) => Application.Run(HandleFetchCommandAsync, o),
    (SubscribeOptions o) => Application.Run(HandleSubscribeCommandAsync, o),
    notParsedFunc: HandleParseError);

static int HandleParseError(IEnumerable<Error> errors)
{
    Console.WriteLine("Failed to parse command line arguments.");
    return 1;
}

static async Task<int> HandleFetchCommandAsync(ILogger logger, FetchOptions options)
{
    logger.Info("Gmail Mailflow CLI starting...");
    logger.Debug("Fetch options: {0}", options);

    using var gmailService = await CreateGmailServiceAsync(logger);
    using var emailFetcher = new EmailFetcher(gmailService);

    var fetchCommand = new FetchCommand(emailFetcher, logger);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        logger.Info("Cancellation requested...");
        cts.Cancel();
    };

    return await fetchCommand.ExecuteAsync(options, cts.Token);
}

 static async Task<int> HandleSubscribeCommandAsync(ILogger logger, SubscribeOptions options)
{
    logger.Info("Gmail Mailflow CLI starting...");
    logger.Debug("Subscribe options: {0}", options);
    
    using var gmailService = await CreateGmailServiceAsync(logger);
    using var emailFetcher = new EmailFetcher(gmailService);
    using var emailSubscriber = new EmailSubscriber();
    using var emailPoller = new EmailPoller(gmailService, emailFetcher);

    var subscribeCommand = new SubscribeCommand(emailSubscriber, emailPoller, gmailService, logger);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        logger.Info("Cancellation requested...");
        cts.Cancel();
    };

    return await subscribeCommand.ExecuteAsync(options, cts.Token);
}

static async Task<GmailService> CreateGmailServiceAsync(ILogger logger)
{
    try
    {
        logger.Info("Authenticating with Google APIs...");

        var scopes = new[] { GmailService.Scope.GmailReadonly };

        // Try to get credentials path from environment variable
        var secretPath = Environment.GetEnvironmentVariable("GCP_CREDENTIALS_PATH");
        if (string.IsNullOrEmpty(secretPath))
        {
            throw new InvalidOperationException("GCP_CREDENTIALS_PATH environment variable not set");
        }
        logger.Debug($"Using credentials from: {secretPath}");

        // Authenticate using the static method
        using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);
        var credential = await Authenticator.Authenticate(secretStream, scopes);

        logger.Debug("Authentication successful.");

        var service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Gmail Client CLI"
        });

        logger.Info("Gmail service initialized successfully.");
        return service;
    }
    catch (Exception ex)
    {
        logger.Error($"Failed to create Gmail service: {ex.Message}");
        throw;
    }
}
