namespace DCiuve.Tools.Gcp.Gmail.Models;

/// <summary>
/// Represents a filter for querying emails.
/// </summary>
public record EmailFilter
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
    /// Combines explicit Query with individual filter properties for comprehensive server-side filtering.
    /// </summary>
    /// <returns>A Gmail query string.</returns>
    public string BuildQuery()
    {
        var queryParts = new List<string>();

        // Start with explicit query if provided
        if (!string.IsNullOrEmpty(Query))
        {
            queryParts.Add($"({Query})");
        }

        // Add individual filter properties
        if (UnreadOnly)
        {
            queryParts.Add("is:unread");
        }

        if (!string.IsNullOrEmpty(FromEmail))
        {
            // Escape email if it contains spaces or special characters
            var escapedFrom = FromEmail.Contains(' ') || FromEmail.Contains('"') 
                ? $"from:\"{FromEmail.Replace("\"", "\\\"")}\""
                : $"from:{FromEmail}";
            queryParts.Add(escapedFrom);
        }

        if (!string.IsNullOrEmpty(Subject))
        {
            // Escape subject if it contains spaces or special characters
            var escapedSubject = Subject.Contains(' ') || Subject.Contains('"')
                ? $"subject:\"{Subject.Replace("\"", "\\\"")}\""
                : $"subject:{Subject}";
            queryParts.Add(escapedSubject);
        }

        if (DateStart.HasValue)
        {
            queryParts.Add($"after:{DateStart.Value:yyyy/MM/dd}");
        }

        if (DateEnd.HasValue)
        {
            queryParts.Add($"before:{DateEnd.Value:yyyy/MM/dd}");
        }

        // Add label filters
        if (LabelIds.Any())
        {
            foreach (var labelId in LabelIds)
            {
                // Escape label if it contains spaces or special characters
                var escapedLabel = labelId.Contains(' ') || labelId.Contains('"')
                    ? $"label:\"{labelId.Replace("\"", "\\\"")}\""
                    : $"label:{labelId}";
                queryParts.Add(escapedLabel);
            }
        }

        return string.Join(" ", queryParts);
    }
    
    /// <summary>
    /// Checks if an email matches the subscription filter.
    /// </summary>
    /// <param name="email">The email to check.</param>
    /// <returns>True if the email matches the filter.</returns>
    public bool Match(EmailMessage email)
    {
        if (this.UnreadOnly && !email.IsUnread)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(this.FromEmail) && 
            !email.From.Contains(this.FromEmail, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(this.Subject) && 
            !email.Subject.Contains(this.Subject, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (this.DateStart.HasValue && email.Date < this.DateStart.Value)
        {
            return false;
        }

        if (this.DateEnd.HasValue && email.Date > this.DateEnd.Value)
        {
            return false;
        }

        if (this.LabelIds.Any() && !this.LabelIds.Any(labelId => email.Labels.Contains(labelId)))
        {
            return false;
        }

        return true;
    }
}
