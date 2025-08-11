using DCiuve.Tools.Gcp.Gmail.Models;
using DCiuve.Tools.Gcp.Gmail.Services;
using DCiuve.Tools.Logging;
using System.Text.Json;

namespace DCiuve.Tools.Gcp.Gmail.Cli.Commands;

/// <summary>
/// Handler for the fetch command.
/// </summary>
public class FetchCommand
{
    private readonly EmailFetcher _emailFetcher;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the FetchCommand class.
    /// </summary>
    /// <param name="emailFetcher">The email fetcher service.</param>
    /// <param name="logger">The logger instance.</param>
    public FetchCommand(EmailFetcher emailFetcher, ILogger logger)
    {
        _emailFetcher = emailFetcher ?? throw new ArgumentNullException(nameof(emailFetcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
            _logger.Info("Starting email fetch operation...");

            var filter = BuildEmailFilter(options);
            var emails = await _emailFetcher.FetchEmailsAsync(filter, cancellationToken);

            if (!emails.Any())
            {
                _logger.Info("No emails found matching the specified criteria.");
                return 0;
            }

            await OutputEmailsAsync(emails, options);
            
            _logger.Info($"Successfully fetched {emails.Count} emails.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing fetch command: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Builds an EmailFilter from the command options.
    /// </summary>
    /// <param name="options">The fetch command options.</param>
    /// <returns>The constructed EmailFilter.</returns>
    private EmailFilter BuildEmailFilter(FetchOptions options)
    {
        var filter = new EmailFilter
        {
            Query = options.Query,
            MaxResults = options.MaxResults,
            UnreadOnly = options.UnreadOnly,
            FromEmail = options.FromEmail,
            Subject = options.Subject,
            IncludeSpamTrash = options.IncludeSpamTrash,
            PageToken = options.PageToken
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
                .ToList();
        }

        return filter;
    }

    /// <summary>
    /// Outputs the emails in the specified format.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    /// <param name="options">The fetch command options.</param>
    private async Task OutputEmailsAsync(List<EmailMessage> emails, FetchOptions options)
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
                OutputToConsole(emails, options.Verbose);
                break;
        }
    }

    /// <summary>
    /// Outputs emails as JSON.
    /// </summary>
    /// <param name="emails">The emails to output.</param>
    private async Task OutputAsJsonAsync(List<EmailMessage> emails)
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
    private async Task OutputAsCsvAsync(List<EmailMessage> emails)
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
    private void OutputToConsole(List<EmailMessage> emails, bool verbose)
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
            
            if (email.Labels.Any())
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
