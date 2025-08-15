# GCP-ExtensionDomain

Shared interfaces for all GCP service projects.

## Purpose

Provides common contracts that all GCP services implement for consistency.

## Interfaces

### IGcpExtensionService
```csharp
public interface IGcpExtensionService
{
    string ApplicationName { get; }
}
```

### IGcpApplicationService  
```csharp
public interface IGcpApplicationService
{
    string ApplicationName { get; }
}
```

## Usage

```csharp
public class MyGcpService : IGcpExtensionService
{
    public string ApplicationName => "My Gmail App";
    
    // ... service implementation
}
```

## Integration

Add reference in your GCP projects:
```xml
<ProjectReference Include="..\..\GCP-ExtensionDomain\src\Gcp.ExtensionDomain.csproj" />
```
