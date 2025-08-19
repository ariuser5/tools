using CommandLine;

namespace DCiuve.Shared.Logging;

public interface ILogSilentOptions
{
	[Option('s', "silent",
		Required = false,
		Default = false,
		HelpText = "Run in silent mode - suppress status and progress messages.")]
	bool Silent { get; set; }
}