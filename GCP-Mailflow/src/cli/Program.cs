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

static async Task<int> HandleFetchCommandAsync(
    ILogger logger,
    FetchOptions options,
    CancellationToken cancellationToken)
{
    logger.Info("Gmail Mailflow CLI starting...");
    logger.Debug("Fetch options: {0}", options);

    var requiredScopes = new[] { GmailService.Scope.GmailReadonly };
    var secretPath = GetSecretPath(options);
    var credential = await AuthenticateAsync(logger, requiredScopes, secretPath, cancellationToken);

    using var gmailClient = CreateGmailClient(logger, credential);
    using var emailFetcher = new EmailFetcher(gmailClient);

    var fetchCommand = new FetchCommand(emailFetcher, logger);
    return await fetchCommand.ExecuteAsync(options, cancellationToken);
}

static async Task<int> HandleSubscribeCommandAsync(
    ILogger logger,
    SubscribeOptions options,
    CancellationToken cancellationToken)
{
    logger.Info("Gmail Mailflow CLI starting...");
    logger.Debug("Subscribe options: {0}", options);

    IGmailClient gmailClient;
    Lazy<ICredential>? pubsubCredential = null;
    try
    {
        string[] requiredScopes = options.UsePullSubscription && options.PubsubSecretPath == null
            ? [GmailService.Scope.GmailReadonly, .. SubscriberServiceApiClient.DefaultScopes]
            : [GmailService.Scope.GmailReadonly];

        var secretPath = GetSecretPath(options);
        var credential = await AuthenticateAsync(logger, requiredScopes, secretPath, cancellationToken);
        gmailClient = CreateGmailClient(logger, credential);
        pubsubCredential = new(() => credential);
    }
    catch (Exception ex) when (options.UsePullSubscription && options.PubsubSecretPath != null)
    {
        logger.Warning("Failed to create Gmail client. " +
            "Process will continue but will not have email access.");
        logger.Debug("Exception details: {0}", ex);

        gmailClient = ThrowingProxy<IGmailClient>.Create(() =>
            throw new InvalidOperationException("Gmail client is not available.", ex));
    }

    using var _ = gmailClient;;

    if (options.UsePullSubscription && options.PubsubSecretPath != null)
    {
        var pubsubAuth = GoogleCredential.FromFile(options.PubsubSecretPath)
            .CreateScoped(SubscriberServiceApiClient.DefaultScopes);
        pubsubCredential = new(() => pubsubAuth);
    }

    pubsubCredential ??= new(() => throw new InvalidOperationException("Pub/Sub credential is not available."));

    var strategyFactory = new SubscriptionStrategyFactory(logger, gmailClient, pubsubCredential);
    var strategy = strategyFactory.CreateStrategy(options);

    var subscribeCommand = new SubscribeCommand(logger, strategy);
    return await subscribeCommand.ExecuteAsync(options, cancellationToken);
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
    var logDisplayScopes = string.Join(", ", scopes);
    try
    {
        logger.Info("Authenticating with Google APIs for scopes [{0}].", logDisplayScopes);

        logger.Debug($"Using credentials from: {secretPath}");
        using var secretStream = new FileStream(secretPath, FileMode.Open, FileAccess.Read);

        var credential = await Authenticator.Authenticate(secretStream, scopes, cancellationToken: cancellationToken);

        logger.Debug("Authentication successful for scopes [{0}].", logDisplayScopes);
        return credential;
    }
    catch (Exception ex)
    {
        logger.Error("Authentication for scopes [{0}] failed: {1}", logDisplayScopes, ex.Message);
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

static int HandleParseError(IEnumerable<Error> errors)
{
    Console.WriteLine("Failed to parse command line arguments.");
    return 1;
}