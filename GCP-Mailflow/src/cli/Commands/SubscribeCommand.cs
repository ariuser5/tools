using CommandLine;
using DCiuve.Gcp.Mailflow.Cli.Subscribe;
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Shared.Logging;
using System.Text.RegularExpressions;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

/// <summary>
/// Command options for subscribing to emails.
/// </summary>
[Verb("subscribe", HelpText = "Subscribe to email notifications and monitor for new emails. The --query and individual filter flags can be combined for comprehensive filtering.")]
public record SubscribeOptions : BaseOptions
{
    // common
    [Option('d', "duration", Required = false, HelpText = "Duration to run subscription (e.g., '1h', '30m', '2d'). Leave empty for indefinite.")]
    public string? Duration { get; set; }
    
    [Option('z', "pubsub-secret-path",
		Required = false,
		HelpText = "Path to Google credentials JSON file for Pub/Sub. " +
			"If not provided, uses the same as --secret-path. " +
            "This is required when the main user credential does not have access to Pub/Sub.")]
	public string? PubsubSecretPath { get; set; }


    [Option("webhook", Required = false, HelpText = "Webhook URL for notifications.")]
    public string? WebhookUrl { get; set; }
    // ***

    // polling specific
    [Option('i', "interval", Required = false, Default = 30, HelpText = "Polling interval in seconds (when not using pull subscription mode).")]
    public int PollingInterval { get; set; } = 30;
    // ***

    // pull specific
    [Option("pull", Required = false, Default = false, HelpText = "Use pull subscription mode (Pub/Sub) instead of polling.")]
    public bool UsePullSubscription { get; set; } = false;

    [Option('n', "subscription", Required = false, HelpText = "Name/ID for this subscription. Required for pull subscription mode.")]
    public string? Name { get; set; }

    [Option('t', "topic", Required = false, HelpText = "Cloud Pub/Sub topic name for pull subscription mode.")]
    public string? TopicName { get; set; }

    [Option("setup-watch", Required = false, Default = false, HelpText = "Setup Gmail watch request automatically (alternative to using PubSubPrimer).")]
    public bool SetupWatch { get; set; } = false;
    // ***
}


public class SubscribeCommand(
    ILogger logger,
    ISubscriptionStrategy subscriptionStrategy)
{
    /// <summary>
    /// Executes the subscribe command.
    /// </summary>
    /// <param name="options">The subscribe command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(SubscribeOptions options, CancellationToken cancellationToken = default)
    {
        logger.Info("Starting email subscription...");

        // Validate options and warn about irrelevant parameters
        if (!ValidateOptions(options)) return 1;

        var subscription = BuildEmailSubscription(options);

        using var durationCts = new CancellationTokenSource();
        if (subscription.EndTime.HasValue)
        {
            var remainingTime = subscription.EndTime.Value - DateTime.UtcNow;
            if (remainingTime > TimeSpan.Zero)
            {
                durationCts.CancelAfter(remainingTime);
                durationCts.Token.Register(() => logger.Info("Subscription duration ended."));
                logger.Info("Subscriber will stop at: {0:yyyy-MM-dd HH:mm:ss}", subscription.EndTime.Value);
            }
            else
            {
                logger.Warning("End time has already passed.");
                return 0;
            }
        }
        else
        {
            logger.Info("Subscriber will run indefinitely. Press Ctrl+C to stop.");
        }

        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);

        subscriptionStrategy.OutputAction = (emails, ct) => OutputEmailsAsync(emails, options, ct);

        try
        {
            await subscriptionStrategy.ExecuteAsync(subscription, combinedCts.Token);
            logger.Info("Email subscription completed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                logger.Debug("Email subscription cancelled by user.");
            else if (durationCts.IsCancellationRequested)
                logger.Debug("Email subscription cancelled due to duration end.");

            return 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Error executing subscribe command: {ex.Message}");
            logger.Debug($"Subscribe command exception details: {ex}");
        }

        return 1;
    }

    /// <summary>
    /// Builds an EmailSubscription from the command options.
    /// </summary>
    /// <param name="options">The subscribe command options.</param>
    /// <param name="endTime">The calculated end time for the subscription.</param>
    /// <returns>The constructed EmailSubscription.</returns>
    private static EmailSubscription BuildEmailSubscription(SubscribeOptions options)
    {
        // Use the extension method to convert to EmailFilter
        // Both --query and individual flags will be combined for comprehensive filtering
        var filter = options.ToEmailFilter();
        
        // Calculate duration if specified
        var duration = ParseDuration(options.Duration);
        var endTime = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : (DateTime?)null;
        
        return new EmailSubscription
        {
            Name = options.Name ?? string.Empty,
            TopicName = options.TopicName ?? string.Empty,
            Filter = filter,
            CallbackUrl = options.WebhookUrl,
            PollingIntervalSeconds = options.PollingInterval,
            EndTime = endTime
        };
    }

    /// <summary>
    /// Validates the subscription options and warns about irrelevant parameters.
    /// </summary>
    /// <param name="options">The subscribe command options.</param>
    /// <returns>True if options are valid, false if there are critical validation errors.</returns>
    private bool ValidateOptions(SubscribeOptions options)
    {
        var isValid = true;
        var irrelevantParams = new List<string>();

        if (options.UsePullSubscription)
        {
            // Pull mode validation
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                logger.Error("Name is required when using pull subscription mode. Use --name option.");
                isValid = false;
            }
            
            if (string.IsNullOrEmpty(options.TopicName))
            {
                logger.Error("Topic name is required when using pull subscription mode. Use --topic option.");
                isValid = false;
            }
            else
            {
                if (options.SetupWatch)
                    logger.Info("Using automatic Gmail watch setup mode.");
                else
                    logger.Info("Using pre-primed topic mode (requires PubSubPrimer setup).");
            }

            // Warn about polling-specific parameters that are irrelevant in pull mode
            if (options.PollingInterval != 30) // 30 is the default
            {
                irrelevantParams.Add("--interval (polling interval is not used in pull mode)");
            }
        }
        else
        {
            // Polling mode validation
            if (options.PollingInterval <= 0)
            {
                logger.Error("Polling interval must be greater than 0 seconds.");
                isValid = false;
            }

            // Warn about pull-specific parameters that are irrelevant in polling mode
            if (!string.IsNullOrEmpty(options.TopicName))
            {
                irrelevantParams.Add("--topic (topic name is not used in polling mode)");
            }

            if (options.SetupWatch)
            {
                irrelevantParams.Add("--setup-watch (watch setup is not used in polling mode)");
            }

            // PubSubSecretPath is only relevant for pull mode (Pub/Sub operations)
            if (!string.IsNullOrEmpty(options.PubsubSecretPath))
            {
                irrelevantParams.Add("--pubsub-secret-path (Pub/Sub credentials are not used in polling mode)");
            }
        }

        // Log warnings for irrelevant parameters
        if (irrelevantParams.Count > 0)
        {
            var mode = options.UsePullSubscription ? "pull" : "polling";
            logger.Warning($"The following parameters are not relevant for {mode} mode and will be ignored:");
            foreach (var param in irrelevantParams)
            {
                logger.Warning($"  • {param}");
            }
        }

        return isValid;
    }

    /// <summary>
    /// Parses a duration string (e.g., "1h", "30m", "2d") into a TimeSpan.
    /// </summary>
    /// <param name="durationString">The duration string.</param>
    /// <returns>The parsed TimeSpan or null if invalid.</returns>
    private static TimeSpan? ParseDuration(string? durationString)
    {
        if (string.IsNullOrEmpty(durationString))
            return null;

        var regex = new Regex(@"^(\d+)([smhd])$", RegexOptions.IgnoreCase);
        var match = regex.Match(durationString.Trim());

        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var value))
            return null;

        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            "d" => TimeSpan.FromDays(value),
            _ => null
        };
    }

    /// <summary>
    /// Outputs the received emails to the specified output file or console.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="options">The subscribe command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task OutputEmailsAsync(
        IReadOnlyCollection<EmailMessage> emails,
        SubscribeOptions options,
        CancellationToken cancellationToken = default)
    {
        // Determine if we're writing to file or console
        var writeToFile = !string.IsNullOrEmpty(options.Output) && options.Output != "-";

        if (writeToFile)
        {
            logger.Debug($"Writing email details to file: {options.Output}");

            // Write to file
            await using var fileStream = new FileStream(options.Output!, FileMode.Append, FileAccess.Write);
            await using var writer = new StreamWriter(fileStream);

            // Temporarily redirect Console.Out to the file
            var originalOut = Console.Out;
            Console.SetOut(writer);

            try
            {
                OutputEmailsToStream(emails, options);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        else
        {
            logger.Debug("Writing email details to console.");
            
            // Write to console (when OutputFile is "-")
            OutputEmailsToStream(emails, options);
        }

        // Send webhook notification if configured
        if (!string.IsNullOrEmpty(options.WebhookUrl))
        {
            logger.Debug($"Sending webhook notification to {options.WebhookUrl} for {emails.Count} new emails...");
            _ = Task.Run(() => SendWebhookNotificationAsync(emails, options.WebhookUrl), cancellationToken);
        }
    }

    /// <summary>
    /// Outputs emails to the current output stream.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="options">The command options.</param>
    private static void OutputEmailsToStream(
        IReadOnlyCollection<EmailMessage> emails,
        SubscribeOptions options)
    {
        foreach (var email in emails)
        {
            Console.WriteLine($"\n🔔 New Email Alert - {DateTime.Now:HH:mm:ss}");
            Console.WriteLine($"Subject: {email.Subject}");
            Console.WriteLine($"From: {email.From}");
            Console.WriteLine($"Date: {email.Date:yyyy-MM-dd HH:mm:ss}");

            if (options.Verbosity > LogLevel.Info)
            {
                Console.WriteLine($"ID: {email.Id}");
                Console.WriteLine($"Labels: {string.Join(", ", email.Labels)}");
                if (!string.IsNullOrEmpty(email.Snippet))
                {
                    Console.WriteLine($"Snippet: {email.Snippet}");
                }
            }

            Console.WriteLine(new string('-', 50));
        }
    }

    /// <summary>
    /// Sends a webhook notification for new emails.
    /// </summary>
    /// <param name="emails">The new emails.</param>
    /// <param name="webhookUrl">The webhook URL.</param>
    private async Task SendWebhookNotificationAsync(IReadOnlyCollection<EmailMessage> emails, string webhookUrl)
    {
        try
        {
            using var httpClient = new HttpClient();

            var payload = new
            {
                timestamp = DateTime.UtcNow,
                emailCount = emails.Count,
                emails = emails.Select(e => new
                {
                    id = e.Id,
                    subject = e.Subject,
                    from = e.From,
                    date = e.Date,
                    isUnread = e.IsUnread
                })
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(webhookUrl, content);

            if (response.IsSuccessStatusCode)
            {
                logger.Info("Webhook notification sent successfully.");
            }
            else
            {
                logger.Warning($"Webhook notification failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error sending webhook notification: {ex.Message}");
        }
    }
}
