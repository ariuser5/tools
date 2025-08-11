using CommandLine;

namespace DCiuve.Tools.Gcp.Gmail.Cli.Commands;

/// <summary>
/// Command options for fetching emails.
/// </summary>
[Verb("fetch", HelpText = "Fetch emails from Gmail based on specified criteria.")]
public class FetchOptions
{
    [Option('q', "query", Required = false, HelpText = "Gmail query string to filter emails.")]
    public string Query { get; set; } = string.Empty;

    [Option('m', "max", Required = false, Default = 10, HelpText = "Maximum number of emails to fetch.")]
    public int MaxResults { get; set; } = 10;

    [Option('u', "unread", Required = false, Default = false, HelpText = "Fetch only unread emails.")]
    public bool UnreadOnly { get; set; } = false;

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

    [Option("include-spam", Required = false, Default = false, HelpText = "Include spam and trash emails.")]
    public bool IncludeSpamTrash { get; set; } = false;

    [Option('o', "output", Required = false, HelpText = "Output format: console, json, csv.")]
    public string OutputFormat { get; set; } = "console";

    [Option("page-token", Required = false, HelpText = "Page token for pagination.")]
    public string? PageToken { get; set; }

    [Option('v', "verbose", Required = false, Default = false, HelpText = "Show detailed email content.")]
    public bool Verbose { get; set; } = false;
}
