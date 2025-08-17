# DCiuve.Shared

Shared utilities: logging, execution pipelines, and CLI application framework.

## Components

### 🔍 Logging (`DCiuve.Shared.Logging`)

Colored console logger with configurable verbosity levels.

```csharp
using DCiuve.Shared.Logging;

var logger = new Logger { Verbosity = LogLevel.Debug };
logger.Info("Operation completed: {0}", result);
```

**Log Levels**: `Quiet` (0) → `Error` (1) → `Warning` (2) → `Info` (3) → `Debug` (4)

### ⚡ ExecutionFlow Pipeline (`DCiuve.Shared.Pipeline`)

Fluent API for data processing pipelines (similar to RxJS operators).

```csharp
using DCiuve.Shared.Pipeline;

var result = ExecutionFlow
    .From("hello world")
    .Map(s => s.ToUpper())
    .Filter(s => s.Length > 5)
    .Catch(ex => "ERROR")
    .Execute();
```

**Key Operators**: `Map`, `Filter`, `Tap`, `Catch`, `Retry`, `Switch`, `FlatMap`

### 🚀 CLI Application (`DCiuve.Shared.Cli`)

Dependency injection-like system for CLI applications.

```csharp
using DCiuve.Shared.Cli;

// Automatic parameter resolution by type
// ILogger auto-injected from ILogVerbosityOptions or default
return Application.Run(MyMethod, options);
```

## Integration

```xml
<ProjectReference Include="path\to\DCiuve.Shared.csproj" />
```

```csharp
using DCiuve.Shared.Logging;
using DCiuve.Shared.Pipeline;
using DCiuve.Shared.Cli;
```
