using CommandLine;
using DCiuve.Gcp.App.Shared.Authentication;
using DCiuve.Gcp.App.Shared.Gmail.CommandLine;
using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.Mailflow.Cli.Commands;

public abstract record BaseOptions : 
	GmailFilterOptionsBase,
	IGcpAppIdentityOptions,
	ILogVerbosityOptions,
	ILogSilentOptions
{

	[Option('o', "output",
		Required = false,
		Default = "-",
		HelpText = "Output for email details. Use '-' for console output. " +
			"If not provided, only status logs are shown.")]
	public string Output { get; set; } = "-";
	
	public LogLevel Verbosity { get; set; } = LogLevel.Info;
    public bool Silent { get; set; } = false;
	public string? ApplicationName { get; set; }
	public string? SecretPath { get; set; }
}