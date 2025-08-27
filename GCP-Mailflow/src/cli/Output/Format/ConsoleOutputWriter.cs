using System.Text;
using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Cli.Output.Format;

public class ConsoleOutputWriter(bool writeDetailed) : IOutputFormattingWriter
{
	public async Task WriteAsync(TextWriter writer, EmailMessage message, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);
		
		if (writeDetailed)
			await WriteConsoleDetailedAsync(writer, message, cancellationToken);
		else
			await WriteConsoleAsync(writer, message, cancellationToken);
	}
	
	public static async Task WriteConsoleAsync(TextWriter writer, EmailMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var sb = new StringBuilder();

		sb.AppendLine("----- Email Message -----");
		sb.AppendLine($"Subject: {message.Subject}");
		sb.AppendLine($"From: {message.From}");
		sb.AppendLine($"To: {message.To}");
		sb.AppendLine($"Date: {message.Date}");
		sb.AppendLine($"Snippet: {message.Snippet}");

		await writer.WriteAsync(sb, cancellationToken);
	}

	public static async Task WriteConsoleDetailedAsync(TextWriter writer, EmailMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var sb = new StringBuilder();

		sb.AppendLine("----- Detailed Email Message -----");
		sb.AppendLine($"ID: {message.Id}");
		sb.AppendLine($"Thread ID: {message.ThreadId}");
		sb.AppendLine($"Subject: {message.Subject}");
		sb.AppendLine($"From: {message.From}");
		sb.AppendLine($"To: {message.To}");
		sb.AppendLine($"Date: {message.Date}");
		sb.AppendLine($"Snippet: {message.Snippet}");
		sb.AppendLine($"Labels: {string.Join(", ", message.Labels)}");
		sb.AppendLine($"Is Unread: {message.IsUnread}");
		sb.AppendLine($"History ID: {message.HistoryId}");
		sb.AppendLine($"Body: {message.Body}");

		await writer.WriteAsync(sb, cancellationToken);
	}
}
