using CommandLine;

namespace DCiuve.Gcp.App.Shared.Gmail.CommandLine;

public interface IGmailQuickFilterOptions
{
	[Option("from", Required = false, HelpText = "Filter emails from specific sender.")]
	string? FromEmail { get; set; }
	
	[Option("subject", Required = false, HelpText = "Filter emails by subject containing this text.")]
	string? Subject { get; set; }
	
	[Option("after", Required = false, HelpText = "Filter emails after this date (yyyy-MM-dd).")]
	string? DateAfter { get; set; }

	[Option("before", Required = false, HelpText = "Filter emails before this date (yyyy-MM-dd).")]
	string? DateBefore { get; set; }

	[Option("labels", Required = false, HelpText = "Comma-separated list of label IDs to filter by. If unspecified, defaults to all labels.")]
	string? Labels { get; set; }

	[Option("unread", Required = false, Default = false, HelpText = "Fetch only unread emails.")]
	bool UnreadOnly { get; set; }

	[Option("include-spam", Required = false, Default = false, HelpText = "Include spam and trash emails.")]
	bool IncludeSpamTrash { get; set; }

	[Option('m', "max", Required = false, Default = 10, HelpText = "Maximum number of emails to process per check.")]
	int MaxResults { get; set; }
}
