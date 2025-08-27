using System.Text.Json;
using DCiuve.Gcp.Mailflow.Cli.Commands;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.Mailflow.Cli.Output.Format;

public class OutputFormattingWriterFactory(BaseOptions options)
{
    public IOutputFormattingWriter Create(string format, JsonSerializerOptions? jsonOptions = null)
    {
        if (string.IsNullOrWhiteSpace(format))
            throw new ArgumentException("format is required", nameof(format));

        return format.Trim().ToLowerInvariant() switch
        {
            "json" => new JsonOutputWriter(jsonOptions),
            "xml" => new XmlOutputWriter(),
            "console" => new ConsoleOutputWriter(writeDetailed: options.Verbosity >= LogLevel.Debug),
            _ => throw new NotSupportedException($"Output format '{format}' is not supported.")
        };
    }
}
