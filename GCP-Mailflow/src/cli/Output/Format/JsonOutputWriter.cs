using System.Text;
using System.Text.Json;
using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Cli.Output.Format;

public class JsonOutputWriter : IOutputFormattingWriter
{
    private readonly JsonSerializerOptions _options;

    public JsonOutputWriter(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions { WriteIndented = false };
    }

    public async Task WriteAsync(TextWriter writer, EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

		var json = JsonSerializer.Serialize(message, _options);
		var sb = new StringBuilder(json);

		await writer.WriteLineAsync(sb, cancellationToken);
    }
}
