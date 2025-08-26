using CommandLine;

namespace DCiuve.Shared.Logging;

public interface ILogVerbosityOptions
{
	[Option('v', "verbosity",
		Required = false,
		Default = LogLevel.Info,
		SetName = "verbosity",
		HelpText = "Set the verbosity level for logging. Default is Info." +
			" Verbosity levels: 0=Quiet, 1=Error, 2=Warning, 3=Info, 4=Debug.")]
	LogLevel Verbosity { get; set; }
}
