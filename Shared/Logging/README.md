# DCiuve.Tools.Logging

A simple, colored console logger for .NET applications with configurable verbosity levels.

## Features

- **Colored Output**: Different colors for different log levels (Error=Red, Warning=Yellow, Info=White, Debug=Gray)
- **Configurable Verbosity**: Set minimum log level to control output detail
- **String Formatting**: Supports `string.Format` style message templates
- **Interface-based**: Implements `ILogger` for dependency injection and testing

## Usage

```csharp
using DCiuve.Tools.Logging;

var logger = new Logger
{
    Verbosity = LogLevel.Debug  // Show all messages
};

logger.Error("Something went wrong: {0}", ex.Message);
logger.Warning("This is a warning about {0}", someValue);
logger.Info("Operation completed successfully");
logger.Debug("Debug info: variable = {0}", debugValue);
```

## Log Levels

- `Error` (0): Only error messages
- `Warning` (1): Warning messages and above
- `Info` (2): Info messages and above (default)
- `Debug` (3): All messages (most verbose)

## Integration

Add as a project reference:

```xml
<ProjectReference Include="..\..\Shared\Logging\src\DCiuve.Tools.Logging.csproj" />
```

Then use the namespace:

```csharp
using DCiuve.Tools.Logging;
```
