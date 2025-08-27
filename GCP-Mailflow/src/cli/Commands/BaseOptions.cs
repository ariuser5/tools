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
		HelpText = "Output file path. Default is '-' to write output to the console. " +
			"Set to an empty string to suppress output.")]
	public string Output { get; set; } = "-";
	
	[Option("output-format", Required = false, HelpText = "Output format: console, json, xml.")]
	public string OutputFormat { get; set; } = "console";

	public LogLevel Verbosity { get; set; } = LogLevel.Info;
	public bool Silent { get; set; } = false;
	public string? ApplicationName { get; set; }
	public string? SecretPath { get; set; }
}