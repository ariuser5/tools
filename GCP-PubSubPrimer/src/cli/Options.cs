using CommandLine;

namespace DCiuve.Gcp.PubSub.Cli;

public abstract class BaseOptions
{
	[Option('p', "project-id", Required = false, HelpText = "GCP Project ID (fallback: GCP_PUBSUB_PROJECTID env var)")]
	public string? ProjectId { get; set; }

	[Option('x', "secret-path", Required = false, HelpText = "Path to Google credentials JSON file (fallback: GCP_CREDENTIALS_PATH env var)")]
	public string? SecretPath { get; set; }

	[Option('a', "application-name", Required = false, HelpText = "Application name for Google API requests (fallback: default application name)")]
	public string? ApplicationName { get; set; }

	[Option('v', "verbose", Required = false, HelpText = "Enable verbose output", Default = 2)]
	public int Verbose { get; set; }
}

[Verb("watch", HelpText = "Create and manage watch requests for GCP services")]
public class WatchOptions : BaseOptions
{
	[Value(0, MetaName = "watch-service", Required = true, HelpText = "GCP service to create watch for (gmail, drive, calendar, sheets)")]
	public string WatchService { get; set; } = string.Empty;

	[Option('t', "topic-id", Required = false, HelpText = "PubSub Topic ID (fallback: GCP_PUBSUB_TOPICID env var)")]
	public string? TopicId { get; set; }
	
	[Option('s', "scopes", Required = false, HelpText = "Comma-separated list of OAuth2 scopes (if not specified, uses service-specific default scopes)")]
	public string? Scopes { get; set; }

	[Option('f', "repeat-frequency", Required = false, HelpText = "Frequency in minutes to repeat the watch request (if not specified, runs once)")]
	public int? Frequency { get; set; }

	[Option("force-new", Required = false, HelpText = "Control watch creation behavior: False=reuse existing, First=force new on first execution only, Always=force new on every execution", Default = ForceNewMode.False)]
	public ForceNewMode ForceNew { get; set; }
}

[Verb("cancel", HelpText = "Cancel active watch requests for GCP services")]
public class CancelOptions : BaseOptions
{
	[Value(0, MetaName = "watch-service", Required = true, HelpText = "GCP service to cancel watch for (gmail, drive, calendar, sheets)")]
	public string WatchService { get; set; } = string.Empty;

	[Option('t', "topic-id", Required = false, HelpText = "PubSub Topic ID for the watch to cancel (fallback: GCP_PUBSUB_TOPICID env var). Note: Gmail cancels ALL watches regardless of topic.")]
	public string? TopicId { get; set; }
}