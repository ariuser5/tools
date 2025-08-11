using CommandLine;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using DCiuve.Tools.Gcp.Auth;
using DCiuve.Tools.Gcp.Gmail.Services;
using DCiuve.Tools.Gcp.Gmail.Cli.Commands;
using DCiuve.Tools.Logging;

namespace DCiuve.Tools.Gcp.Gmail.Cli;

/// <summary>
/// Main program class for the Gmail Manager CLI.
/// </summary>
class Program
{
    private static readonly ILogger _logger = new Logger();

    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    static async Task<int> Main(string[] args)
    {
        try
        {
            _logger.Info("Gmail Manager CLI starting...");

            var result = await Parser.Default.ParseArguments<FetchOptions, SubscribeOptions>(args)
                .MapResult(
                    (FetchOptions opts) => HandleFetchCommandAsync(opts),
                    (SubscribeOptions opts) => HandleSubscribeCommandAsync(opts),
                    errs => Task.FromResult(1));

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unhandled exception: {ex.Message}");
            if (_logger is Logger logger && logger.Verbosity <= LogLevel.Debug)
            {
                _logger.Debug($"Stack trace: {ex.StackTrace}");
            }
            return 1;
        }
    }

    /// <summary>
    /// Handles the fetch command.
    /// </summary>
    /// <param name="options">Fetch command options.</param>
    /// <returns>Exit code.</returns>
    private static async Task<int> HandleFetchCommandAsync(FetchOptions options)
    {
        using var gmailService = await CreateGmailServiceAsync();
        using var emailFetcher = new EmailFetcher(gmailService, _logger);
        
        var fetchCommand = new FetchCommand(emailFetcher, _logger);
        
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        return await fetchCommand.ExecuteAsync(options, cts.Token);
    }

    /// <summary>
    /// Handles the subscribe command.
    /// </summary>
    /// <param name="options">Subscribe command options.</param>
    /// <returns>Exit code.</returns>
    private static async Task<int> HandleSubscribeCommandAsync(SubscribeOptions options)
    {
        using var gmailService = await CreateGmailServiceAsync();
        using var emailFetcher = new EmailFetcher(gmailService, _logger);
        using var emailSubscriber = new EmailSubscriber(gmailService, emailFetcher, _logger);
        
        var subscribeCommand = new SubscribeCommand(emailSubscriber, _logger);
        
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            _logger.Info("Cancellation requested...");
        };

        return await subscribeCommand.ExecuteAsync(options, cts.Token);
    }

    /// <summary>
    /// Creates and configures a Gmail service instance.
    /// </summary>
    /// <returns>The configured Gmail service.</returns>
    private static async Task<GmailService> CreateGmailServiceAsync()
    {
        try
        {
            _logger.Info("Authenticating with Google APIs...");

            var scopes = new[] { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailModify };
            
            // Try to get credentials path from environment variable
            var secretPath = Environment.GetEnvironmentVariable("GCP_CREDENTIALS_PATH");
            if (string.IsNullOrEmpty(secretPath))
            {
                throw new InvalidOperationException("GCP_CREDENTIALS_PATH environment variable not set");
            }

            // Authenticate using the static method
            using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);
            var credential = await Authenticator.Authenticate(secretStream, scopes);

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Gmail Manager CLI"
            });

            _logger.Info("Gmail service initialized successfully.");
            return service;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create Gmail service: {ex.Message}");
            throw;
        }
    }
}
