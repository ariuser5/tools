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
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Cli.Output.Format;

var result = Parser.Default.ParseArguments<FetchOptions, SubscribeOptions>(args);

// Handle parsing errors early
if (result.Tag == ParserResultType.NotParsed)
{
    // Exit early if parsing failed
    return HandleParseError(((NotParsed<object>)result).Errors);
}

using var cts = new CancellationTokenSource();
var app = Application.CreateBasic();

var options = (BaseOptions)result.Value;
app.RegisterDependency<ILogVerbosityOptions>(options);
app.RegisterDependency<ILogSilentOptions>(options);
app.RegisterDependency<TextWriter>(p => CreateOutputWiter(p, options));
app.RegisterDependency<IOutputFormattingWriter>(p => CreateFormattingOutputWriter(options));

var logger = app.GetDependency<ILogger>();
logger.Info("Gmail Mailflow CLI starting...");

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
    IOutputFormattingWriter formattingWriter,
    TextWriter outputWriter,
    CancellationToken cancellationToken)
{
    logger.Debug("Fetch options: {0}", options);

    var requiredScopes = new[] { GmailService.Scope.GmailReadonly };
    var secretPath = GetSecretPath(options);
    var credential = await AuthenticateAsync(logger, requiredScopes, secretPath, cancellationToken);

    using var gmailClient = CreateGmailClient(logger, credential);
    using var emailFetcher = new EmailFetcher(gmailClient);
    
    var emailsCount = 0;
	async Task Output(EmailMessage email, CancellationToken ct)
    {
        cancellationToken.ThrowIfCancellationRequested();
        emailsCount++;
        logger.Debug("Writing email number '{0}' with id '{1}' to output.", emailsCount, email.Id);
        await formattingWriter.WriteAsync(outputWriter, email, ct);
	}

	var fetchCommand = new FetchCommand(logger, emailFetcher, Output);
    var fetchResult = await fetchCommand.ExecuteAsync(options, cancellationToken);

    logger.Debug($"Successfully fetched {emailsCount} emails.");
    return fetchResult;
}

static async Task<int> HandleSubscribeCommandAsync(
    ILogger logger,
    SubscribeOptions options,
    IOutputFormattingWriter formattingWriter,
    TextWriter outputWriter,
    CancellationToken cancellationToken)
{
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

    using var _ = gmailClient; ;

    if (options.UsePullSubscription && options.PubsubSecretPath != null)
    {
        var pubsubAuth = GoogleCredential.FromFile(options.PubsubSecretPath)
            .CreateScoped(SubscriberServiceApiClient.DefaultScopes);
        pubsubCredential = new(() => pubsubAuth);
    }

    pubsubCredential ??= new(() => throw new InvalidOperationException("Pub/Sub credential is not available."));

    var strategyFactory = new SubscriptionStrategyFactory(logger, gmailClient, pubsubCredential);
    var strategy = strategyFactory.CreateStrategy(options);

    var emailsCount = 0;
    async Task Output(EmailMessage email, CancellationToken ct)
    {
        cancellationToken.ThrowIfCancellationRequested();
        emailsCount++;
        logger.Debug("Writing email number '{0}' with id '{1}' to output.", emailsCount, email.Id);
        await formattingWriter.WriteAsync(outputWriter, email, ct);
    }

    var subscribeCommand = new SubscribeCommand(logger, strategy, Output);
    var subscribeResult = await subscribeCommand.ExecuteAsync(options, cancellationToken);

    logger.Debug($"Successfully received and processed {emailsCount} emails.");
    return subscribeResult;
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

static TextWriter CreateOutputWiter(IDependencyProvider depsProvider, BaseOptions options)
{
    var logger = depsProvider.GetDependency<ILogger>();
    
    TextWriter textWriter;
    if (options.Output == "-")
    {
        logger.Debug("Writing output to console.");
        textWriter = Console.Out;
    }
    else if (string.IsNullOrWhiteSpace(options.Output))
    {
        logger.Warning("Output is suppressed because of the explicit empty output path.");
        textWriter = TextWriter.Null;
    }
    else
    {
        logger.Debug($"Writing output to file: {options.Output}");
        using var fileStream = new FileStream(options.Output, FileMode.Create, FileAccess.Write);
        using var fileWriter = new StreamWriter(fileStream);
        textWriter = fileWriter;
    }

    return textWriter;
}

static IOutputFormattingWriter CreateFormattingOutputWriter(BaseOptions options)
{
    var outputWriterFactory = new OutputFormattingWriterFactory(options);
    return outputWriterFactory.Create(options.OutputFormat);
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