using CommandLine;

namespace DCiuve.Shared.Logging;

public interface ILogSilentOptions
{
	[Option("silent", Required = false, HelpText = "Enable silent mode (no output).")]
	bool Silent { get; set; }
}