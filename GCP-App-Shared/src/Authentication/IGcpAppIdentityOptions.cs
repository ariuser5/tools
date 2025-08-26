using CommandLine;

namespace DCiuve.Gcp.App.Shared.Authentication;

public interface IGcpAppIdentityOptions : IGcpAuthOptions
{
	[Option("application-name", Required = false, HelpText = "Application name for Google API requests (fallback: default application name)")]
	public string? ApplicationName { get; set; }
}