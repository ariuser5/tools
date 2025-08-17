using DCiuve.Shared.Logging;

namespace DCiuve.Shared.Cli;

public static class Application
{
	/// <summary>
	/// Runs a method with automatic dependency injection-like parameter resolution.
	/// ILogger is automatically constructed if not provided in args.
	/// </summary>
	/// <param name="method">The method to invoke</param>
	/// <param name="args">Arguments to resolve against method parameters</param>
	/// <returns>Exit code (0 for success, 1 for error)</returns>
	public static int Run(Delegate method, params object[] args)
	{
		try
		{
			var resolvedArgs = ResolveMethodParameters(method, args);
			var result = method.DynamicInvoke(resolvedArgs);
			
			return result switch
			{
				int intValue => intValue,
				Task<int> task => task.GetAwaiter().GetResult(),
				Task => 0, // Task without return value = success
				null => 0, // void method = success
				_ => throw new InvalidOperationException($"Method must return int, Task<int>, Task, or void. Got: {result.GetType()}")
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			return 1;
		}
	}
	
	/// <summary>
	/// Resolves method parameters by matching types from the provided arguments.
	/// Special handling for ILogger which is auto-constructed if not provided.
	/// </summary>
	private static object?[] ResolveMethodParameters(Delegate method, object[] args)
	{
		var methodInfo = method.Method;
		var parameters = methodInfo.GetParameters();
		var resolvedArgs = new object?[parameters.Length];
		
		// Keep track of used arguments to avoid double-assignment
		var usedArgIndices = new HashSet<int>();
		
		for (int paramIndex = 0; paramIndex < parameters.Length; paramIndex++)
		{
			var parameter = parameters[paramIndex];
			var parameterType = parameter.ParameterType;
			
			// Special case: ILogger auto-construction
			if (parameterType == typeof(ILogger))
			{
				resolvedArgs[paramIndex] = ResolveLogger(args);
				continue;
			}
			
			// Find first matching argument by type
			object? matchedArg = null;
			for (int argIndex = 0; argIndex < args.Length; argIndex++)
			{
				if (usedArgIndices.Contains(argIndex))
					continue;
				
				var arg = args[argIndex];
				if (arg == null)
					continue;
					
				var argType = arg.GetType();
				
				// Exact type match or assignable (inheritance/interface)
				if (parameterType.IsAssignableFrom(argType))
				{
					matchedArg = arg;
					usedArgIndices.Add(argIndex);
					break;
				}
			}
			
			if (matchedArg != null)
			{
				resolvedArgs[paramIndex] = matchedArg;
			}
			else if (parameter.HasDefaultValue)
			{
				resolvedArgs[paramIndex] = parameter.DefaultValue;
			}
			else if (parameterType.IsValueType)
			{
				resolvedArgs[paramIndex] = Activator.CreateInstance(parameterType);
			}
			else
			{
				throw new InvalidOperationException(
					$"Cannot resolve parameter '{parameter.Name}' of type '{parameterType.Name}'. " +
					$"No matching argument found and parameter has no default value.");
			}
		}
		
		return resolvedArgs;
	}
	
	/// <summary>
	/// Resolves ILogger by finding an existing ILogger in args, or finding ILogVerbosityOptions to build one, or creates default logger.
	/// </summary>
	private static ILogger ResolveLogger(object[] args)
	{
		// First, look for an existing ILogger instance in the arguments
		var existingLogger = args.OfType<ILogger>().FirstOrDefault();
		if (existingLogger != null)
		{
			return existingLogger;
		}
		
		// No existing logger found, look for ILogVerbosityOptions to build one
		var verbosityOptions = args.OfType<ILogVerbosityOptions>().FirstOrDefault();
		if (verbosityOptions != null)
		{
			return BuildLogger(verbosityOptions);
		}
		
		// No verbosity options found either, create default logger
		return new Logger { Verbosity = LogLevel.Info };
	}
	
	/// <summary>
	/// Builds a logger configured with the specified verbosity options.
	/// </summary>
	private static ILogger BuildLogger(ILogVerbosityOptions options)
	{
		var logger = new Logger();
		logger.ConfigureWithOptions(options);
		return logger;
	}
}