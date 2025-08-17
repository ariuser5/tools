using CommandLine;

namespace DCiuve.Shared.Logging;

public interface ILogVerbosityOptions
{
	[Option('v', "verbosity", Required = false, HelpText = "Set the verbosity level for logging. Default is Info.", Default = LogLevel.Info)	]
	LogLevel Verbosity { get; set; }
}
