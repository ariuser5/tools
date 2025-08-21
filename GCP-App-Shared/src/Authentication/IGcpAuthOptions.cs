using CommandLine;

namespace DCiuve.Gcp.App.Shared.Authentication;

public interface  IGcpAuthOptions
{
	[Option('x', "secret-path", Required = false, HelpText = "Path to Google credentials JSON file (fallback: GCP_CREDENTIALS_PATH env var)")]
	public string? SecretPath { get; set; }
}
