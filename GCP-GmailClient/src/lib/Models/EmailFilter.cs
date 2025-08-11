namespace DCiuve.Tools.Gcp.Gmail.Models;

/// <summary>
/// Represents a filter for querying emails.
/// </summary>
public class EmailFilter
{
    /// <summary>
    /// Gets or sets the Gmail query string.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to include spam and trash emails.
    /// </summary>
    public bool IncludeSpamTrash { get; set; } = false;

    /// <summary>
    /// Gets or sets the label IDs to filter by.
    /// </summary>
    public List<string> LabelIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the page token for pagination.
    /// </summary>
    public string? PageToken { get; set; }

    /// <summary>
    /// Gets or sets whether this is an unread only filter.
    /// </summary>
    public bool UnreadOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets the sender email filter.
    /// </summary>
    public string? FromEmail { get; set; }

    /// <summary>
    /// Gets or sets the subject filter.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the date range start.
    /// </summary>
    public DateTime? DateStart { get; set; }

    /// <summary>
    /// Gets or sets the date range end.
    /// </summary>
    public DateTime? DateEnd { get; set; }

    /// <summary>
    /// Builds a Gmail query string from the filter properties.
    /// </summary>
    /// <returns>A Gmail query string.</returns>
    public string BuildQuery()
    {
        var queryParts = new List<string>();

        if (!string.IsNullOrEmpty(Query))
        {
            queryParts.Add(Query);
        }

        if (UnreadOnly)
        {
            queryParts.Add("is:unread");
        }

        if (!string.IsNullOrEmpty(FromEmail))
        {
            queryParts.Add($"from:{FromEmail}");
        }

        if (!string.IsNullOrEmpty(Subject))
        {
            queryParts.Add($"subject:\"{Subject}\"");
        }

        if (DateStart.HasValue)
        {
            queryParts.Add($"after:{DateStart.Value:yyyy/MM/dd}");
        }

        if (DateEnd.HasValue)
        {
            queryParts.Add($"before:{DateEnd.Value:yyyy/MM/dd}");
        }

        return string.Join(" ", queryParts);
    }
}
