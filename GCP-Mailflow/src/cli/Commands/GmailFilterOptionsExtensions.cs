
using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

static class GmailFilterOptionsExtensions
{
	/// <summary>
	/// Converts GmailFilterOptions to an EmailFilter.
	/// All fields are passed through - conflict resolution between Query and individual flags is handled by EmailFilter.
	/// </summary>
	/// <param name="options">The GmailFilterOptions to convert.</param>
	/// <param name="maxResults">Maximum number of results (from command-specific options).</param>
	/// <param name="pageToken">Page token for pagination (from command-specific options).</param>
	/// <returns>An EmailFilter configured with all provided options.</returns>
	public static EmailFilter ToEmailFilter(this GmailFilterOptions options, int maxResults = 10, string? pageToken = null)
	{
		var filter = new EmailFilter
		{
			Query = options.Query,
			MaxResults = maxResults,
			UnreadOnly = options.UnreadOnly,
			FromEmail = options.FromEmail,
			Subject = options.Subject,
			IncludeSpamTrash = options.IncludeSpamTrash,
			PageToken = pageToken
		};

		// Parse date filters
		if (!string.IsNullOrEmpty(options.DateAfter) && DateTime.TryParse(options.DateAfter, out var dateAfter))
		{
			filter.DateStart = dateAfter;
		}

		if (!string.IsNullOrEmpty(options.DateBefore) && DateTime.TryParse(options.DateBefore, out var dateBefore))
		{
			filter.DateEnd = dateBefore;
		}

		// Parse label IDs
		if (!string.IsNullOrEmpty(options.Labels))
		{
			filter.LabelIds = options.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(l => l.Trim())
				.Where(l => !string.IsNullOrEmpty(l))
				.ToList();
		}

		return filter;
	}
}