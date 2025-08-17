using CommandLine;
using DCiuve.Gcp.Mailflow.Models;
using DCiuve.Gcp.Mailflow.Services;
using DCiuve.Shared.Logging;
using System.Text.Json;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

/// <summary>
/// Command for fetching emails from Gmail.
/// </summary>
[Verb("fetch", HelpText = "Fetch emails from Gmail based on specified criteria. The --query and individual filter flags can be combined for comprehensive filtering.")]
public record FetchOptions : BaseOptions
{
	[Option('m', "max", Required = false, Default = 10, HelpText = "Maximum number of emails to fetch.")]
	public int MaxResults { get; set; } = 10;

	[Option("output-format", Required = false, HelpText = "Output format: console, json, csv.")]
	public string OutputFormat { get; set; } = "console";

	[Option("page-token", Required = false, HelpText = "Page token for pagination.")]
	public string? PageToken { get; set; }
}


public class FetchCommand(EmailFetcher emailFetcher, ILogger logger)
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
            var filter = options.ToEmailFilter(options.MaxResults, options.PageToken);

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

            await OutputEmailsAsync(emails, options);

            logger.Info($"Successfully fetched {emails.Count} emails.");
            return 0;
        }
        catch (Exception ex)
        {
            // Errors are always shown, even through QuietLogger
            logger.Error($"Error executing fetch command: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Outputs the emails in the specified format.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="options">The fetch command options.</param>
    private static async Task OutputEmailsAsync(List<EmailMessage> emails, FetchOptions options)
    {
        // Determine if we're writing to file or stdout
        var writeToFile = !string.IsNullOrEmpty(options.Output) && options.Output != "-";
        
        if (writeToFile)
        {
            // Write to file
            await using var fileStream = new FileStream(options.Output, FileMode.Create, FileAccess.Write);
            await using var writer = new StreamWriter(fileStream);
            
            // Temporarily redirect Console.Out to the file
            var originalOut = Console.Out;
            Console.SetOut(writer);
            
            try
            {
                await OutputEmailsToStreamAsync(emails, options);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        else
        {
            // Write to stdout (default)
            await OutputEmailsToStreamAsync(emails, options);
        }
    }

    /// <summary>
    /// Outputs emails to the current output stream based on format.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="options">The fetch command options.</param>
    private static async Task OutputEmailsToStreamAsync(List<EmailMessage> emails, FetchOptions options)
    {
        switch (options.OutputFormat.ToLowerInvariant())
        {
            case "json":
                await OutputAsJsonAsync(emails);
                break;
            case "csv":
                await OutputAsCsvAsync(emails);
                break;
            case "console":
            default:
                OutputToConsole(emails, options.Verbosity > LogLevel.Info);
                break;
        }
    }

    /// <summary>
    /// Outputs emails as JSON.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    private static async Task OutputAsJsonAsync(List<EmailMessage> emails)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(emails, jsonOptions);
        Console.WriteLine(json);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Outputs emails as CSV.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    private static async Task OutputAsCsvAsync(List<EmailMessage> emails)
    {
        Console.WriteLine("Id,ThreadId,Subject,From,To,Date,IsUnread,Labels,Snippet");

        foreach (var email in emails)
        {
            var labels = string.Join(";", email.Labels);
            var snippet = email.Snippet.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", " ");

            Console.WriteLine($"\"{email.Id}\",\"{email.ThreadId}\",\"{email.Subject}\",\"{email.From}\",\"{email.To}\",\"{email.Date:yyyy-MM-dd HH:mm:ss}\",{email.IsUnread},\"{labels}\",\"{snippet}\"");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Outputs emails to the console.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="verbose">Whether to show detailed information.</param>
    private static void OutputToConsole(List<EmailMessage> emails, bool verbose)
    {
        for (int i = 0; i < emails.Count; i++)
        {
            var email = emails[i];

            Console.WriteLine($"\n--- Email {i + 1} of {emails.Count} ---");
            Console.WriteLine($"ID: {email.Id}");
            Console.WriteLine($"Subject: {email.Subject}");
            Console.WriteLine($"From: {email.From}");
            Console.WriteLine($"To: {email.To}");
            Console.WriteLine($"Date: {email.Date:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Unread: {(email.IsUnread ? "Yes" : "No")}");

            if (email.Labels.Count > 0)
            {
                Console.WriteLine($"Labels: {string.Join(", ", email.Labels)}");
            }

            if (!string.IsNullOrEmpty(email.Snippet))
            {
                Console.WriteLine($"Snippet: {email.Snippet}");
            }

            if (verbose && !string.IsNullOrEmpty(email.Body))
            {
                Console.WriteLine("\n--- Body ---");
                Console.WriteLine(email.Body);
            }
        }
    }
}
