namespace DCiuve.Gcp.App.Shared.Gmail.CommandLine;

public record GmailFilterOptionsBase : IGmailQueryOptions, IGmailQuickFilterOptions
{
	public string Query { get; set; } = string.Empty;
	public string? FromEmail { get; set; }
	public string? Subject { get; set; }
	public string? DateAfter { get; set; }
	public string? DateBefore { get; set; }
	public string? Labels { get; set; }
	public bool UnreadOnly { get; set; } = false;
	public bool IncludeSpamTrash { get; set; } = false;
	public int MaxResults { get; set; } = 10;
}
