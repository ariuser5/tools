using CommandLine;
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

/// <summary>
/// Command for fetching emails from Gmail.
/// </summary>
[Verb("fetch", HelpText = "Fetch emails from Gmail based on specified criteria. The --query and individual filter flags can be combined for comprehensive filtering.")]
public record FetchOptions : BaseOptions
{
	[Option("page-token", Required = false, HelpText = "Page token for pagination.")]
	public string? PageToken { get; set; }
}


public class FetchCommand(
    ILogger logger,
    EmailFetcher emailFetcher,
    Func<EmailMessage, CancellationToken, Task> output)
{
    /// <summary>
    /// Executes the fetch command.
    /// </summary>
    /// <param name="options">The fetch command options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(FetchOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Info("Starting email fetch operation...");

            // Use the extension method to convert to EmailFilter
            // Both --query and individual flags will be combined for comprehensive filtering
            var filter = options.ToEmailFilter(options.PageToken);

            if (filter.MaxResults <= 0)
            {
                logger.Warning("MaxResults is set to 0 or less. No emails will be fetched.");
                return 0;
            }

            var emails = await emailFetcher.FetchEmailsAsync(filter, cancellationToken);

            if (emails.Count == 0)
            {
                logger.Info("No emails found matching the specified criteria.");
                return 0;
            }
            
            foreach (var email in emails)
            {
                await output(email, cancellationToken);
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Error executing fetch command: {ex.Message}");
            return 1;
        }
    }
}
