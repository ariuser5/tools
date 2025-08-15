using CommandLine;

namespace DCiuve.Tools.Gcp.Mailflow.Cli.Commands;

public abstract class GmailFilterOptions
{
	[Option('q', "query", Required = false, HelpText = "Gmail query string to filter emails. Can be combined with individual filter flags for comprehensive filtering.")]
	public string Query { get; set; } = string.Empty;

	[Option('f', "from", Required = false, HelpText = "Filter emails from specific sender.")]
	public string? FromEmail { get; set; }

	[Option('s', "subject", Required = false, HelpText = "Filter emails by subject containing this text.")]
	public string? Subject { get; set; }

	[Option("after", Required = false, HelpText = "Filter emails after this date (yyyy-MM-dd).")]
	public string? DateAfter { get; set; }

	[Option("before", Required = false, HelpText = "Filter emails before this date (yyyy-MM-dd).")]
	public string? DateBefore { get; set; }

	[Option('l', "labels", Required = false, HelpText = "Comma-separated list of label IDs to filter by.")]
	public string? Labels { get; set; }

	[Option('u', "unread", Required = false, Default = false, HelpText = "Fetch only unread emails.")]
	public bool UnreadOnly { get; set; } = false;

	[Option("include-spam", Required = false, Default = false, HelpText = "Include spam and trash emails.")]
	public bool IncludeSpamTrash { get; set; } = false;
}
