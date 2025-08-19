using CommandLine;

namespace DCiuve.Shared.Logging;

public interface ILogVerbosityOptions
{
	[Option('v', "verbosity",
		Required = false,
		Default = LogLevel.Info,
		HelpText = "Set the verbosity level for logging. Default is Info.")]
	LogLevel Verbosity { get; set; }
}
