# GCP-App-Shared

A shared library containing common utilities, constants, and models for Google Cloud Platform applications in the DCiuve ecosystem.

## Features

- **Constants**: Shared constants for GCP services, OAuth scopes, and application settings
- **Extensions**: Utility extension methods for string manipulation and GCP resource parsing
- **Models**: Common data models for GCP resource names and other shared structures

## Target Framework

- .NET 6.0 (for broad compatibility)

## Dependencies

- Google.Apis.Core
- Microsoft.Extensions.Logging.Abstractions

## Usage

```csharp
using DCiuve.Gcp.Shared;
using DCiuve.Gcp.Shared.Extensions;
using DCiuve.Gcp.Shared.Models;

// Use shared constants
var gmailScope = Constants.Auth.Scopes.GmailReadonly;
var defaultLabel = Constants.Gmail.Labels.Inbox;

// Parse GCP resource names
var topicName = "projects/my-project/topics/my-topic";
var projectId = topicName.ExtractProjectId(); // "my-project"
var resourceId = topicName.ExtractResourceId("topics"); // "my-topic"

// Parse full resource names
var resource = GcpResourceName.Parse("projects/my-project/subscriptions/my-sub");
Console.WriteLine(resource.ProjectId); // "my-project"
Console.WriteLine(resource.ResourceType); // "subscriptions"
Console.WriteLine(resource.ResourceId); // "my-sub"
```

## Integration

This library is included in:
- GCP-Mailflow solution
- GCP-PubSub solution

Add reference to your project:
```xml
<ProjectReference Include="..\..\GCP-App-Shared\src\GCP-App-Shared.csproj" />
```
