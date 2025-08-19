using CommandLine;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

public abstract record BaseOptions : GmailFilterOptions, ILogVerbosityOptions, ILogSilentOptions
{

	[Option('o', "output",
		Required = false,
		Default = "-",
		HelpText = "Output for email details. Use '-' for console output. If not provided, only status logs are shown.")]
	public string Output { get; set; } = "-";
	
	public LogLevel Verbosity { get; set; } = LogLevel.Info;
    public bool Silent { get; set; } = false;
}