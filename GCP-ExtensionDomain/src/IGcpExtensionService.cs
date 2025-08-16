namespace DCiuve.Gcp.ExtensionDomain;

/// <summary>
/// Interface for services that have an associated GCP application name.
/// This is typically used for services that wrap Google API clients.
/// </summary>
public interface IGcpExtensionService : IDisposable
{
    /// <summary>
    /// Gets the application name used for Google API requests.
    /// This corresponds to the ApplicationName property in Google API client initializers.
    /// </summary>
    string ApplicationName { get; }
}
