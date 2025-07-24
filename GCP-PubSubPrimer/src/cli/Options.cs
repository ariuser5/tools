using CommandLine;

namespace DCiuve.Tools.Gcp.PubSub.Cli;

public class Options
{
	[Value(0, MetaName = "watch-service", Required = true, HelpText = "GCP service to create watch for (gmail, drive, calendar, sheets)")]
	public string WatchService { get; set; } = string.Empty;

	[Option('p', "project-id", Required = false, HelpText = "GCP Project ID (fallback: PUBSUB_GMAILPRIMER_PROJECTID env var)")]
	public string? ProjectId { get; set; }

	[Option('t', "topic-id", Required = false, HelpText = "PubSub Topic ID (fallback: PUBSUB_GMAILPRIMER_TOPICID env var)")]
	public string? TopicId { get; set; }
	
	[Option('s', "scopes", Required = false, HelpText = "Comma-separated list of OAuth2 scopes (if not specified, uses service-specific default scopes)")]
	public string? Scopes { get; set; }

	[Option('x', "secret-path", Required = false, HelpText = "Path to Google credentials JSON file (fallback: GOOGLE_CREDENTIALS_PATH env var)")]
	public string? SecretPath { get; set; }

	[Option('f', "repeat-frequency", Required = false, HelpText = "Frequency in minutes to repeat the watch request (if not specified, runs once)")]
	public int? Frequency { get; set; }

	[Option('v', "verbose", Required = false, HelpText = "Enable verbose output", Default = 2)]
	public int Verbose { get; set; }
}