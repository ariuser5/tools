using DCiuve.Tools.Gcp.Gmail.Models;
using DCiuve.Tools.Gcp.Gmail.Services;
using DCiuve.Tools.Logging;
using System.Text.RegularExpressions;

namespace DCiuve.Tools.Gcp.Gmail.Cli.Commands;

/// <summary>
/// Handler for the subscribe command.
/// </summary>
public class SubscribeCommand
{
    private readonly EmailSubscriber _emailSubscriber;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the SubscribeCommand class.
    /// </summary>
    /// <param name="emailSubscriber">The email subscriber service.</param>
    /// <param name="logger">The logger instance.</param>
    public SubscribeCommand(EmailSubscriber emailSubscriber, ILogger logger)
    {
        _emailSubscriber = emailSubscriber ?? throw new ArgumentNullException(nameof(emailSubscriber));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
            _logger.Info($"Starting email subscription: {options.Name}");

            // Validate options
            if (options.UsePushNotifications)
            {
                if (string.IsNullOrEmpty(options.TopicName))
                {
                    _logger.Error("Topic name is required when using push notifications. Use --topic option.");
                    return 1;
                }

                if (options.SetupWatch)
                {
                    _logger.Info("Using automatic Gmail watch setup mode.");
                }
                else
                {
                    _logger.Info("Using pre-primed topic mode (requires PubSubPrimer setup).");
                }
            }

            var subscription = BuildEmailSubscription(options);
            
            // Set up event handler for new emails
            _emailSubscriber.NewEmailsReceived += (sender, emails) => OnNewEmailsReceived(emails, options);

            // Calculate duration if specified
            var duration = ParseDuration(options.Duration);
            var endTime = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : (DateTime?)null;

            if (options.UsePushNotifications && !string.IsNullOrEmpty(options.TopicName))
            {
                await SetupPushNotificationsAsync(subscription, options, cancellationToken);
            }
            else
            {
                await StartPollingAsync(subscription, endTime, cancellationToken);
            }

            _logger.Info("Email subscription completed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Email subscription cancelled by user.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing subscribe command: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Builds an EmailSubscription from the command options.
    /// </summary>
    /// <param name="options">The subscribe command options.</param>
    /// <returns>The constructed EmailSubscription.</returns>
    private EmailSubscription BuildEmailSubscription(SubscribeOptions options)
    {
        var filter = new EmailFilter
        {
            Query = options.Query,
            MaxResults = options.MaxResults,
            UnreadOnly = options.UnreadOnly,
            FromEmail = options.FromEmail,
            Subject = options.Subject
        };

        // Parse date filters
        if (!string.IsNullOrEmpty(options.DateAfter) && DateTime.TryParse(options.DateAfter, out var dateAfter))
        {
            filter.DateStart = dateAfter;
        }

        if (!string.IsNullOrEmpty(options.DateBefore) && DateTime.TryParse(options.DateBefore, out var dateBefore))
        {
            filter.DateEnd = dateBefore;
        }

        // Parse label IDs
        if (!string.IsNullOrEmpty(options.Labels))
        {
            filter.LabelIds = options.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .ToList();
        }

        return new EmailSubscription
        {
            Name = options.Name,
            TopicName = options.TopicName ?? string.Empty,
            Filter = filter,
            CallbackUrl = options.WebhookUrl,
            PollingIntervalSeconds = options.PollingInterval,
            IsActive = true
        };
    }

    /// <summary>
    /// Sets up push notifications for the subscription.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="options">The command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SetupPushNotificationsAsync(EmailSubscription subscription, SubscribeOptions options, CancellationToken cancellationToken)
    {
        try
        {
            if (options.SetupWatch)
            {
                _logger.Info("Setting up Gmail watch request automatically...");
                var watchResponse = await _emailSubscriber.SetupPushNotificationAsync(subscription, cancellationToken);
                var expirationTime = watchResponse.Expiration.HasValue 
                    ? DateTimeOffset.FromUnixTimeMilliseconds((long)watchResponse.Expiration.Value)
                    : DateTimeOffset.Now.AddHours(24); // Default expiration
                _logger.Info($"Gmail watch setup successful. Expiration: {expirationTime}");
            }
            else
            {
                _logger.Info($"Starting push notification listener for topic: {options.TopicName}");
                _logger.Info("Note: Make sure you have already primed the Pub/Sub topic using GCP-PubSubPrimer.");
            }

            // Start the push notification listener
            await _emailSubscriber.StartPushNotificationListenerAsync(subscription, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Push notification listener stopped due to cancellation.");
        }
        finally
        {
            if (options.SetupWatch)
            {
                try
                {
                    _logger.Info("Cleaning up Gmail watch...");
                    await _emailSubscriber.StopPushNotificationAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Error stopping Gmail watch: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Starts polling for emails.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="endTime">The time to stop polling (null for indefinite).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task StartPollingAsync(EmailSubscription subscription, DateTime? endTime, CancellationToken cancellationToken)
    {
        _logger.Info($"Starting email polling every {subscription.PollingIntervalSeconds} seconds");
        
        if (endTime.HasValue)
        {
            _logger.Info($"Polling will stop at: {endTime.Value:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            _logger.Info("Polling will run indefinitely. Press Ctrl+C to stop.");
        }

        // Create a cancellation token that respects the duration
        using var durationCts = new CancellationTokenSource();
        if (endTime.HasValue)
        {
            var remainingTime = endTime.Value - DateTime.UtcNow;
            if (remainingTime > TimeSpan.Zero)
            {
                durationCts.CancelAfter(remainingTime);
            }
            else
            {
                _logger.Warning("End time has already passed.");
                return;
            }
        }

        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCts.Token);
        
        await _emailSubscriber.StartPollingAsync(subscription, combinedCts.Token);
    }

    /// <summary>
    /// Handles new emails received event.
    /// </summary>
    /// <param name="emails">The new emails received.</param>
    /// <param name="options">The command options.</param>
    private void OnNewEmailsReceived(List<EmailMessage> emails, SubscribeOptions options)
    {
        _logger.Info($"Received {emails.Count} new emails!");

        foreach (var email in emails)
        {
            Console.WriteLine($"\n🔔 New Email Alert - {DateTime.Now:HH:mm:ss}");
            Console.WriteLine($"Subject: {email.Subject}");
            Console.WriteLine($"From: {email.From}");
            Console.WriteLine($"Date: {email.Date:yyyy-MM-dd HH:mm:ss}");
            
            if (options.Verbose)
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

        // Send webhook notification if configured
        if (!string.IsNullOrEmpty(options.WebhookUrl))
        {
            _ = Task.Run(() => SendWebhookNotificationAsync(emails, options.WebhookUrl));
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
                _logger.Info("Webhook notification sent successfully.");
            }
            else
            {
                _logger.Warning($"Webhook notification failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error sending webhook notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a duration string (e.g., "1h", "30m", "2d") into a TimeSpan.
    /// </summary>
    /// <param name="durationString">The duration string.</param>
    /// <returns>The parsed TimeSpan or null if invalid.</returns>
    private TimeSpan? ParseDuration(string? durationString)
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
}
