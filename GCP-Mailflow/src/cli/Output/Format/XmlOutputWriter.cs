using System.Text;
using System.Xml.Serialization;
using DCiuve.Gcp.Mailflow.Models;

namespace DCiuve.Gcp.Mailflow.Cli.Output.Format;

public class XmlOutputWriter : IOutputFormattingWriter
{
    public async Task WriteAsync(TextWriter writer, EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

		var serializer = new XmlSerializer(typeof(EmailMessage));
		var sb = new StringBuilder();
		using var stringWriter = new StringWriter(sb);
		serializer.Serialize(stringWriter, message);
		var xml = stringWriter.ToString();

		await writer.WriteLineAsync(sb, cancellationToken);
    }
}
