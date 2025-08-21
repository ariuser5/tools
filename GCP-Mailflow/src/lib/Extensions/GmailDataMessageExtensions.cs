using System.Text;
using DCiuve.Gcp.Mailflow.Models;
using Google.Apis.Gmail.v1.Data;

namespace DCiuve.Gcp.Mailflow.Extensions;

public static class GmailDataMessageExtensions
{
	public static EmailMessage ToEmailMessage(this Message message)
	{
		var headers = message.Payload?.Headers ?? new List<MessagePartHeader>();

		return new EmailMessage
		{
			Id = message.Id ?? string.Empty,
			ThreadId = message.ThreadId ?? string.Empty,
			Subject = GetHeaderValue(headers, "Subject") ?? "No Subject",
			From = GetHeaderValue(headers, "From") ?? "Unknown Sender",
			To = GetHeaderValue(headers, "To") ?? string.Empty,
			Date = ParseEmailDate(GetHeaderValue(headers, "Date")),
			Snippet = message.Snippet ?? string.Empty,
			Labels = message.LabelIds?.ToList() ?? new List<string>(),
			IsUnread = message.LabelIds?.Contains("UNREAD") ?? false,
			Body = ExtractBodyContent(message.Payload)
		};
	}
	
    private static string ExtractBodyContent(MessagePart? payload)
    {
        if (payload == null)
            return string.Empty;

        var body = new StringBuilder();

        // Check if this part has body data
        if (payload.Body?.Data != null)
        {
            var decodedData = Convert.FromBase64String(payload.Body.Data.Replace('-', '+').Replace('_', '/'));
            body.Append(Encoding.UTF8.GetString(decodedData));
        }

        // Recursively check parts
        if (payload.Parts != null)
        {
            foreach (var part in payload.Parts)
            {
                // Prefer plain text, but fall back to HTML
                if (part.MimeType == "text/plain" || part.MimeType == "text/html")
                {
                    var partBody = ExtractBodyContent(part);
                    if (!string.IsNullOrEmpty(partBody))
                    {
                        body.AppendLine(partBody);
                    }
                }
            }
        }

        return body.ToString().Trim();
    }

	private static string? GetHeaderValue(IList<MessagePartHeader> headers, string name)
	{
		return headers.FirstOrDefault(
			h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)
		)?.Value;
	}
	
	private static DateTime ParseEmailDate(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return DateTime.UtcNow;

        return DateTimeOffset.TryParse(dateString, out var date) ? date.DateTime : DateTime.UtcNow;
    }
}