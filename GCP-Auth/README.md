# GCP-Auth

Simple Google Cloud authentication library for .NET applications.

## Usage

```csharp
using DCiuve.Tools.Gcp.Auth;

// From file stream
using var secretStream = new FileStream(credentialsPath, FileMode.Open);
var credential = await Authenticator.Authenticate(secretStream, scopes);

// From JSON string
var credential = await Authenticator.Authenticate(jsonContent, scopes);

// From ClientSecrets
var credential = await Authenticator.Authenticate(clientSecrets, scopes);
```

## Integration with Google APIs

```csharp
var gmailService = new GmailService(new BaseClientService.Initializer()
{
    HttpClientInitializer = credential,
    ApplicationName = "My App",
});
```

## Features

- Multiple credential input formats (stream, string, ClientSecrets)
- Automatic token refresh
- Secure credential storage
- Cross-platform support
