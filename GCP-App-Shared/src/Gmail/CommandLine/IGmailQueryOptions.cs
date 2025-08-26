using CommandLine;

namespace DCiuve.Gcp.App.Shared.Gmail.CommandLine;

public interface IGmailQueryOptions
{
	[Option('q', "query", Required = false, HelpText = "Gmail query string to filter emails. Can be combined with individual filter flags for comprehensive filtering.")]
	string Query { get; set; }	
}