using CommandLine;
using DCiuve.Gcp.Mailflow.Cli.Services;
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Shared.Logging;
using Google.Apis.Gmail.v1;
using System.Text.RegularExpressions;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

/// <summary>
/// Command options for subscribing to emails.
/// </summary>
[Verb("subscribe", HelpText = "Subscribe to email notifications and monitor for new emails. The --query and individual filter flags can be combined for comprehensive filtering.")]
public record SubscribeOptions : BaseOptions
{
    // poll/push common
    [Option('d', "duration", Required = false, HelpText = "Duration to run subscription (e.g., '1h', '30m', '2d'). Leave empty for indefinite.")]
    public string? Duration { get; set; }

    [Option("webhook", Required = false, HelpText = "Webhook URL for notifications.")]
    public string? WebhookUrl { get; set; }
    // ***

    // polling specific
    [Option('i', "interval", Required = false, Default = 30, HelpText = "Polling interval in seconds (when not using push notifications).")]
    public int PollingInterval { get; set; } = 30;
    // ***

    // push specific
    [Option("push", Required = false, Default = false, HelpText = "Use push notifications instead of polling.")]
    public bool UsePushNotifications { get; set; } = false;

    [Option('n', "name", Required = false, HelpText = "Name/ID for this subscription. If not provided, a unique ID will be auto-generated.")]
    public string? Name { get; set; }

    [Option('t', "topic", Required = false, HelpText = "Cloud Pub/Sub topic name for push notifications.")]
    public string? TopicName { get; set; }

    [Option("setup-watch", Required = false, Default = false, HelpText = "Setup Gmail watch request automatically (alternative to using PubSubPrimer).")]
    public bool SetupWatch { get; set; } = false;
    // ***
}


public class SubscribeCommand(
    EmailSubscriber emailSubscriber,
    EmailPoller emailPoller,
    GmailService gmailService,
    ILogger logger)
{
    /// <summary>
    /// Executes the subscribe command.
    /// </summary>
    /// <param name="options">The subscribe command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(SubscribeOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Info("Starting email subscription...");

            // Validate options and warn about irrelevant parameters
            if (!ValidateOptions(options))
            {
                return 1;
            }

            // Calculate duration if specified
            var duration = ParseDuration(options.Duration);
            var endTime = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : (DateTime?)null;

            var subscription = BuildEmailSubscription(options, endTime);
            logger.Info($"Subscription ID: {subscription.Name}");

            if (options.UsePushNotifications && !string.IsNullOrEmpty(options.TopicName))
            {
                await SetupPushNotificationsAsync(subscription, options, cancellationToken);
            }
            else
            {
                await StartPollingAsync(subscription, options, cancellationToken);
            }

            logger.Info("Email subscription completed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            logger.Info("Email subscription cancelled by user.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Error executing subscribe command: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Builds an EmailSubscription from the command options.
    /// </summary>
    /// <param name="options">The subscribe command options.</param>
    /// <param name="endTime">The calculated end time for the subscription.</param>
    /// <returns>The constructed EmailSubscription.</returns>
    private static EmailSubscriptionParams BuildEmailSubscription(SubscribeOptions options, DateTime? endTime)
    {
        // Auto-generate name if not provided
        var subscriptionName = options.Name
            ?? $"subscription-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

        // Use the extension method to convert to EmailFilter
        // Both --query and individual flags will be combined for comprehensive filtering
        var filter = options.ToEmailFilter();

        return new EmailSubscriptionParams
        {
            Name = subscriptionName,
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

        if (options.UsePushNotifications)
        {
            // Push mode validation
            if (string.IsNullOrEmpty(options.TopicName))
            {
                logger.Error("Topic name is required when using push notifications. Use --topic option.");
                isValid = false;
            }
            else
            {
                if (options.SetupWatch)
                    logger.Info("Using automatic Gmail watch setup mode.");
                else
                    logger.Info("Using pre-primed topic mode (requires PubSubPrimer setup).");
            }

            // Warn about polling-specific parameters that are irrelevant in push mode
            if (options.PollingInterval != 30) // 30 is the default
            {
                irrelevantParams.Add("--interval (polling interval is not used in push mode)");
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

            // Warn about push-specific parameters that are irrelevant in polling mode
            if (!string.IsNullOrEmpty(options.TopicName))
            {
                irrelevantParams.Add("--topic (topic name is not used in polling mode)");
            }

            if (options.SetupWatch)
            {
                irrelevantParams.Add("--setup-watch (watch setup is not used in polling mode)");
            }
        }

        // Log warnings for irrelevant parameters
        if (irrelevantParams.Count > 0)
        {
            var mode = options.UsePushNotifications ? "push" : "polling";
            logger.Warning($"The following parameters are not relevant for {mode} mode and will be ignored:");
            foreach (var param in irrelevantParams)
            {
                logger.Warning($"  • {param}");
            }
        }

        return isValid;
    }

    /// <summary>
    /// Sets up push notifications for the subscription.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="options">The command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SetupPushNotificationsAsync(
        EmailSubscriptionParams subscription,
        SubscribeOptions options,
        CancellationToken cancellationToken)
    {
            logger.Warning("Push notifications are not yet implemented. Please use polling mode.");
        
        if (options.SetupWatch)
        {
            logger.Info("Setting up Gmail watch and starting email monitoring...");

            // Create a cancellation token source for coordinated cancellation
            using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start watch management in background
            var watchTask = StartWatchManagementAsync(options, subscription.EndTime, watchCts.Token);

            try
            {
                // Start email monitoring as the master task
                await StartEmailMonitoringAsync(subscription, options, cancellationToken);
            }
            catch
            {
                // If monitoring fails, cancel the watch management
                logger.Info("Stopping watch management due to monitoring failure...");
                watchCts.Cancel();
                throw;
            }
            finally
            {
                // Ensure watch management is stopped and we wait for cleanup
                watchCts.Cancel();
                try
                {
                    await watchTask;
                }
                catch (OperationCanceledException) { /* Expected */ }
                catch (Exception ex)
                {
                    logger.Warning($"Error during watch management cleanup: {ex.Message}");
                }
            }
        }
        else
        {
            await StartEmailMonitoringAsync(subscription, options, cancellationToken);
        }
    }

    /// <summary>
    /// Starts Gmail watch management in the background.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="endTime">When to stop managing watches (null for indefinite).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task StartWatchManagementAsync(
        SubscribeOptions options,
        DateTime? endTime,
        CancellationToken cancellationToken)
    {
        GmailWatchManager? watchManager = null;
        try
        {
            var watchAppName = options.Name ?? AppDomain.CurrentDomain.FriendlyName;
            watchManager = new GmailWatchManager(gmailService, logger, watchAppName);
            await watchManager.StartWatchManagementAsync(
                topicName: options.TopicName!,
                labelIds: Utils.ParseLabels(options.Labels ?? string.Empty),
                endTime: endTime,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error($"Gmail watch management failed: {ex.Message}");
            throw;
        }
        finally
        {
            if (watchManager != null)
            {
                await watchManager.StopWatchManagementAsync();
                watchManager.Dispose();
            }
        }
    }

    /// <summary>
    /// Starts email monitoring using push notifications via EmailSubscriber.
    /// This method sets up real push notification handling instead of polling.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="options">The subscribe command options for output configuration.</param>
    private async Task StartEmailMonitoringAsync(
        EmailSubscriptionParams subscription,
        SubscribeOptions options,
        CancellationToken cancellationToken)
    {
        logger.Info("Starting email monitoring (push notification mode)...");

        // TODO: Implement push notification listener setup
        // This would typically involve:
        // 1. Setting up Pub/Sub subscription listener
        // 2. Configuring message handler for incoming notifications
        // 3. Processing Gmail history IDs from push notifications
        // 4. Fetching actual email content when notifications arrive

        try
        {
            // Start the push notification listener
            var messageStream = emailSubscriber.StartPushNotificationListenerAsync(
                subscription, cancellationToken);

            logger.Info("Push notification listener started successfully");

            // Create a cancellation token that respects the duration
            using var durationCts = new CancellationTokenSource();
            if (subscription.EndTime.HasValue)
            {
                var remainingTime = subscription.EndTime.Value - DateTime.UtcNow;
                if (remainingTime > TimeSpan.Zero)
                {
                    durationCts.CancelAfter(remainingTime);
                    logger.Info($"Push notifications will stop at: {subscription.EndTime.Value:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    logger.Warning("End time has already passed.");
                    return;
                }
            }
            else
            {
                logger.Info("Push notifications will run indefinitely. Press Ctrl+C to stop.");
            }

            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);

            // Process messages from the push notification stream
            await foreach (var messageBatch in messageStream.WithCancellation(combinedCts.Token))
            {
                if (messageBatch.Count > 0)
                {
                    logger.Info($"Received {messageBatch.Count} new emails!");

                    // Only output email details if output file is specified
                    if (!string.IsNullOrEmpty(options.Output))
                    {
                        await OutputEmailsAsync(messageBatch, options, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.Info("Push notification monitoring cancelled.");
        }
        catch (NotImplementedException)
        {
            logger.Error("Push notifications are not yet implemented in EmailSubscriber.");
            throw;
        }
        finally
        {
            // Stop the push notification listener
            try
            {
                emailSubscriber.Stop();
                logger.Info("Push notification listener stopped.");
            }
            catch (Exception ex)
            {
                logger.Warning($"Error stopping push notification listener: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Starts polling for emails.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="options">The command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task StartPollingAsync(EmailSubscriptionParams subscription, SubscribeOptions options, CancellationToken cancellationToken)
    {
        var messageStream = emailPoller.StartPollingAsync(subscription, cancellationToken);
        logger.Info($"Starting email polling every {subscription.PollingIntervalSeconds} seconds");

        if (subscription.EndTime.HasValue)
        {
            logger.Info($"Polling will stop at: {subscription.EndTime.Value:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            logger.Info("Polling will run indefinitely. Press Ctrl+C to stop.");
        }

        // Create a cancellation token that respects the duration
        using var durationCts = new CancellationTokenSource();
        if (subscription.EndTime.HasValue)
        {
            var remainingTime = subscription.EndTime.Value - DateTime.UtcNow;
            if (remainingTime > TimeSpan.Zero)
            {
                durationCts.CancelAfter(remainingTime);
            }
            else
            {
                logger.Warning("End time has already passed.");
                return;
            }
        }

        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);

        await foreach (var emails in messageStream.WithCancellation(combinedCts.Token))
        {
            if (emails.Count > 0)
            {
                logger.Info($"Received {emails.Count} new emails!");

                // Only output email details if output file is specified
                if (!string.IsNullOrEmpty(options.Output))
                {
                    await OutputEmailsAsync(emails, options, cancellationToken);
                }
            }
        }
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
    private async Task OutputEmailsAsync(List<EmailMessage> emails, SubscribeOptions options, CancellationToken cancellationToken = default)
    {
        // Determine if we're writing to file or console
        var writeToFile = !string.IsNullOrEmpty(options.Output) && options.Output != "-";

        if (writeToFile)
        {
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
            // Write to console (when OutputFile is "-")
            OutputEmailsToStream(emails, options);
        }

        // Send webhook notification if configured
        if (!string.IsNullOrEmpty(options.WebhookUrl))
        {
            _ = Task.Run(() => SendWebhookNotificationAsync(emails, options.WebhookUrl), cancellationToken);
        }
    }

    /// <summary>
    /// Outputs emails to the current output stream.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="options">The command options.</param>
    private static void OutputEmailsToStream(List<EmailMessage> emails, SubscribeOptions options)
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
    private async Task SendWebhookNotificationAsync(List<EmailMessage> emails, string webhookUrl)
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
