using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Cli.Output.Format;

public interface IOutputFormattingWriter
{
    /// <summary>
    /// Write a single email message to the provided TextWriter using the writer's format.
    /// </summary>
    Task WriteAsync(TextWriter writer, EmailMessage message, CancellationToken cancellationToken = default);
}
