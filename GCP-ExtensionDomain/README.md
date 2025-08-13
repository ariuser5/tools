# GCP-ExtensionDomain

Domain interfaces and contracts shared across all GCP extensions and services.

## Purpose

This library provides the domain layer abstractions that ensure consistency across different GCP extension projects:

- **Interfaces**: Common contracts for GCP services
- **Models**: Shared data structures
- **Constants**: Common configuration values

## Interfaces

### IGcpApplicationService

Exposes the ApplicationName used by Google API clients. Implement this interface in services that wrap Google API clients to provide access to the application name.

```csharp
public class MyGmailService : IGcpApplicationService
{
    public string ApplicationName => "My Gmail App";
    
    // ... rest of implementation
}
```

## Usage

Add reference to this project in your GCP extension projects:

```xml
<ProjectReference Include="..\..\GCP-ExtensionDomain\src\DCiuve.Tools.Gcp.ExtensionDomain.csproj" />
```

## Target Framework

- .NET 6.0 (compatible with all current GCP extension projects)
