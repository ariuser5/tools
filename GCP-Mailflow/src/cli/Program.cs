using CommandLine;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Gcp.Mailflow.Cli.Commands;
using DCiuve.Gcp.Auth;
using DCiuve.Shared.Cli;
using DCiuve.Shared.Logging;
using DCiuve.Gcp.Mailflow.Cli.Subscribe;
using DCiuve.Shared.Reflection;
using DCiuve.Gcp.Shared.Gmail;
using DCiuve.Gcp.ExtensionDomain.Gmail;
using Google.Cloud.PubSub.V1;
using Google.Apis.Auth.OAuth2;

using var cts = new CancellationTokenSource();
var app = Application.CreateBasic();

var result = Parser.Default.ParseArguments<FetchOptions, SubscribeOptions>(args);
var options = (BaseOptions)result.Value;

app.RegisterDependency<ILogVerbosityOptions>(options);
app.RegisterDependency<ILogSilentOptions>(options);

var logger = app.GetDependency<ILogger>();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    logger.Info($"Cancellation requested by user: {Environment.UserName}");
    cts.Cancel();
};

return result.MapResult(
    (FetchOptions o) => app.Run(HandleFetchCommandAsync, o, cts.Token),
    (SubscribeOptions o) => app.Run(HandleSubscribeCommandAsync, o, cts.Token),
    notParsedFunc: HandleParseError);

int HandleParseError(IEnumerable<Error> errors)
{
    logger.Error("Failed to parse command line arguments.");
    return 1;
}

static async Task<int> HandleFetchCommandAsync(
    ILogger logger,
    FetchOptions options,
    CancellationToken cancellationToken = default)
{
    logger.Info("Gmail Mailflow CLI starting...");
    logger.Debug("Fetch options: {0}", options);

    var requiredScopes = new[] { GmailService.Scope.GmailReadonly };
    var secretPath = GetSecretPath(options);
    var credential = await AuthenticateAsync(logger, requiredScopes, secretPath, cancellationToken);

    using var gmailClient = CreateGmailClient(logger, credential);
    using var emailFetcher = new EmailFetcher(gmailClient);

    var fetchCommand = new FetchCommand(emailFetcher, logger);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        logger.Info($"Cancellation requested by user: {Environment.UserName}");
        cts.Cancel();
    };

    return await fetchCommand.ExecuteAsync(options, cts.Token);
}

static async Task<int> HandleSubscribeCommandAsync(
    ILogger logger,
    SubscribeOptions options,
    CancellationToken cancellationToken = default)
{
    logger.Info("Gmail Mailflow CLI starting...");
    logger.Debug("Subscribe options: {0}", options);

    IGmailClient gmailClient;
    Lazy<ICredential>? pubsubCredential = null;
    try
    {
        string[] requiredScopes = options.UsePushNotifications && options.PubsubSecretPath == null
            ? [GmailService.Scope.GmailReadonly, .. SubscriberServiceApiClient.DefaultScopes]
            : [GmailService.Scope.GmailReadonly];

        var secretPath = GetSecretPath(options);
        var credential = await AuthenticateAsync(logger, requiredScopes, secretPath, cancellationToken);
        gmailClient = CreateGmailClient(logger, credential);
        pubsubCredential = new(() => credential);
    }
    catch (Exception ex) when (options.UsePushNotifications && options.PubsubSecretPath != null)
    {
        logger.Warning("Failed to create Gmail client. " +
            "Process will continue but will not have email access.");
        logger.Debug("Exception details: {0}", ex);

        gmailClient = ThrowingProxy<IGmailClient>.Create(() =>
            throw new InvalidOperationException("Gmail client is not available.", ex));
    }

    using var _ = gmailClient;;

    if (options.UsePushNotifications && options.PubsubSecretPath != null)
    {
        var pubsubAuth = await AuthenticateAsync(
            logger,
            scopes: [.. SubscriberServiceApiClient.DefaultScopes],
            secretPath: options.PubsubSecretPath,
            cancellationToken: cancellationToken);
        pubsubCredential = new(() => pubsubAuth);
    }

    pubsubCredential ??= new Lazy<ICredential>(() => 
        throw new InvalidOperationException("Pub/Sub credential is not available."));

    var strategyFactory = new SubscriptionStrategyFactory(logger, gmailClient, pubsubCredential);
    var strategy = strategyFactory.CreateStrategy(options);

    var subscribeCommand = new SubscribeCommand(logger, strategy);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        logger.Info($"Cancellation requested by user: {Environment.UserName}");
        cts.Cancel();
    };

    return await subscribeCommand.ExecuteAsync(options, cts.Token);
}

static IGmailClient CreateGmailClient(ILogger logger, UserCredential credential)
{
    try
    {
        var service = new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Gmail Client CLI"
        });

        logger.Info("Gmail service initialized successfully.");
        return new GmailClientAdapter(service, disposeInner: true);
    }
    catch (Exception ex)
    {
        logger.Error($"Failed to create Gmail service: {ex.Message}");
        throw;
    }
}

static async Task<UserCredential> AuthenticateAsync(
    ILogger logger,
    string[] scopes,
    string secretPath,
    CancellationToken cancellationToken = default)
{
    try
    {
        logger.Info("Authenticating with Google APIs for scopes [{0}].", scopes);
        
        logger.Debug($"Using credentials from: {secretPath}");
        using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);

        var credential = await Authenticator.Authenticate(secretStream, scopes, cancellationToken: cancellationToken);

        logger.Debug("Authentication successful for scopes [{0}].", scopes);
        return credential;
    }
    catch (Exception ex)
    {
        logger.Error("Authentication for scopes [{0}] failed: {1}", scopes, ex.Message);
        logger.Debug("Authentication exception details: {0}", ex);
        throw;
    }
}

static string GetSecretPath(BaseOptions options)
{
    var result = options.SecretPath ?? Environment.GetEnvironmentVariable("GCP_CREDENTIALS_PATH");
    if (string.IsNullOrEmpty(result))
    {
        throw new InvalidOperationException("GCP_CREDENTIALS_PATH environment variable not set");
    }
    return result;
}
