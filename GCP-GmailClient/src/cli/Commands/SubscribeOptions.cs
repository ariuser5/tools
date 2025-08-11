using CommandLine;

namespace DCiuve.Tools.Gcp.Gmail.Cli.Commands;

/// <summary>
/// Command options for subscribing to emails.
/// </summary>
[Verb("subscribe", HelpText = "Subscribe to email notifications and monitor for new emails.")]
public class SubscribeOptions
{
    [Option('n', "name", Required = true, HelpText = "Name for this subscription.")]
    public string Name { get; set; } = string.Empty;

    [Option('t', "topic", Required = false, HelpText = "Cloud Pub/Sub topic name for push notifications.")]
    public string? TopicName { get; set; }

    [Option('i', "interval", Required = false, Default = 30, HelpText = "Polling interval in seconds (when not using push notifications).")]
    public int PollingInterval { get; set; } = 30;

    [Option('q', "query", Required = false, HelpText = "Gmail query string to filter emails.")]
    public string Query { get; set; } = string.Empty;

    [Option('u', "unread", Required = false, Default = false, HelpText = "Monitor only unread emails.")]
    public bool UnreadOnly { get; set; } = false;

    [Option('f', "from", Required = false, HelpText = "Monitor emails from specific sender.")]
    public string? FromEmail { get; set; }

    [Option('s', "subject", Required = false, HelpText = "Monitor emails with subject containing this text.")]
    public string? Subject { get; set; }

    [Option("after", Required = false, HelpText = "Monitor emails after this date (yyyy-MM-dd).")]
    public string? DateAfter { get; set; }

    [Option("before", Required = false, HelpText = "Monitor emails before this date (yyyy-MM-dd).")]
    public string? DateBefore { get; set; }

    [Option('l', "labels", Required = false, HelpText = "Comma-separated list of label IDs to filter by.")]
    public string? Labels { get; set; }

    [Option('m', "max", Required = false, Default = 50, HelpText = "Maximum number of emails to process per check.")]
    public int MaxResults { get; set; } = 50;

    [Option("webhook", Required = false, HelpText = "Webhook URL for notifications.")]
    public string? WebhookUrl { get; set; }

    [Option("duration", Required = false, HelpText = "Duration to run subscription (e.g., '1h', '30m', '2d'). Leave empty for indefinite.")]
    public string? Duration { get; set; }

    [Option('v', "verbose", Required = false, Default = false, HelpText = "Show detailed output.")]
    public bool Verbose { get; set; } = false;

    [Option("push", Required = false, Default = false, HelpText = "Use push notifications instead of polling.")]
    public bool UsePushNotifications { get; set; } = false;

    [Option("setup-watch", Required = false, Default = false, HelpText = "Setup Gmail watch request automatically (alternative to using PubSubPrimer).")]
    public bool SetupWatch { get; set; } = false;
}
