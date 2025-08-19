using System.Diagnostics.CodeAnalysis;
using DCiuve.Shared.Logging;

namespace DCiuve.Shared.Cli;

public class Application : IDependencyProvider
{
	private readonly Dictionary<Type, object> _instances = new();
	private readonly Dictionary<Type, Func<IDependencyProvider, object>> _factories = new();

	/// <summary>
	/// Creates a new Application instance with basic pre-registered dependencies.
	/// This includes a default ILogger instance.
	/// </summary>
	/// <returns>A new Application instance with basic dependencies registered</returns>
	public static Application CreateBasic()
	{
		var app = new Application();
		app.RegisterDependency<ILogger>(provider =>
		{
			var stdLogger = new Logger();
			if (provider.TryGetDependency<ILogVerbosityOptions>(out var verbosityOptions))
			{
				stdLogger.ConfigureWithOptions(verbosityOptions);
			}

			if (provider.TryGetDependency<ILogSilentOptions>(out var silentOptions))
			{
				return new SuppressibleLogger(stdLogger)
				{
					isSilent = silentOptions.Silent
				};
			}

			return stdLogger;
		});
		return app;
	}

	/// <summary>
	/// Registers a dependency instance for the specified type.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <param name="instance">The dependency instance</param>
	/// <returns>This Application instance for method chaining</returns>
	public Application RegisterDependency<T>(T instance)
	{
		if (instance == null)
			throw new ArgumentNullException(nameof(instance));

		_instances[typeof(T)] = instance;
		return this;
	}

	/// <summary>
	/// Registers a dependency instance for a specific interface.
	/// </summary>
	/// <typeparam name="TInterface">The interface type</typeparam>
	/// <typeparam name="TImplementation">The implementation type</typeparam>
	/// <param name="instance">The dependency instance</param>
	/// <returns>This Application instance for method chaining</returns>
	public Application RegisterDependency<TInterface, TImplementation>(TImplementation instance)
		where TImplementation : class, TInterface
	{
		_instances[typeof(TInterface)] = instance ?? throw new ArgumentNullException(nameof(instance));
		return this;
	}

	/// <summary>
	/// Registers a dependency factory for the specified type.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <param name="factory">Factory function to create the dependency</param>
	/// <returns>This Application instance for method chaining</returns>
	public Application RegisterDependency<T>(Func<T> factory) where T : class
	{
		if (factory == null)
			throw new ArgumentNullException(nameof(factory));

		_factories[typeof(T)] = _ => factory();
		return this;
	}

	/// <summary>
	/// Registers a dependency factory for the specified type with access to the service provider.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <param name="factory">Factory function to create the dependency with service provider access</param>
	/// <returns>This Application instance for method chaining</returns>
	public Application RegisterDependency<T>(Func<IDependencyProvider<T>, T> factory) where T : class
	{
		if (factory == null)
			throw new ArgumentNullException(nameof(factory));

		var targetType = typeof(T);

		// Store the previous registration for decorator pattern support
		object? previousInstance = null;
		Func<IDependencyProvider, object>? previousFactory = null;

		if (_instances.TryGetValue(targetType, out previousInstance))
		{
			// Previous was a direct instance - remove it since we're replacing with factory
			_instances.Remove(targetType);
		}
		else if (_factories.TryGetValue(targetType, out previousFactory))
		{
			// Previous was a factory - we'll wrap it
		}

		// Create a wrapper that provides access to the previous registration
		_factories[targetType] = provider =>
		{
			var scopedProvider = new DecoratorDependencyProvider<T>(provider, previousInstance, previousFactory);
			return factory(scopedProvider);
		};
		return this;
	}

	/// <summary>
	/// Gets a registered dependency of the specified type.
	/// Only returns directly registered instances (not factory-created dependencies).
	/// Factory-created dependencies are only resolved during Run() execution for thread safety.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <returns>The dependency instance if directly registered</returns>
	/// <exception cref="InvalidOperationException">Thrown when the dependency is not registered or is factory-registered</exception>
	public T GetDependency<T>() where T : class
	{
		// Only return directly registered instances, not factory-created ones
		if (_instances.TryGetValue(typeof(T), out var service))
			return (T)service;

		throw new InvalidOperationException($"Dependency of type '{typeof(T).Name}' is not registered as a direct instance. Use TryGetDependency for safe access or ensure the dependency is registered with RegisterDependency(instance).");
	}

	/// <summary>
	/// Tries to get a registered dependency of the specified type.
	/// Only returns directly registered instances (not factory-created dependencies).
	/// Factory-created dependencies are only resolved during Run() execution for thread safety.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <param name="dependency">The retrieved dependency, or null if not found</param>
	/// <returns>True if the dependency was found and is directly registered, false otherwise</returns>
	public bool TryGetDependency<T>([NotNullWhen(true)] out T? dependency) where T : class
	{
		if (_instances.TryGetValue(typeof(T), out var service))
		{
			dependency = (T)service;
			return true;
		}

		dependency = null;
		return false;
	}

	/// <summary>
	/// Runs a method with automatic dependency injection-like parameter resolution.
	/// Parameters are resolved from registered dependencies.
	/// Creates a fresh dependency container for each execution to ensure thread safety.
	/// </summary>
	/// <param name="method">The method to invoke</param>
	/// <param name="additionalArgs">Additional arguments to resolve against method parameters</param>
	/// <returns>Exit code (0 for success, 1 for error)</returns>
	public int Run(Delegate method, params object[] additionalArgs)
	{
		try
		{
			var resolvedArgs = ResolveDependencies(method, additionalArgs);
			var result = method.DynamicInvoke(resolvedArgs);
			
			return ProcessResult(result);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			return 1;
		}
	}

	/// <summary>
	/// Runs an async method with automatic dependency injection-like parameter resolution.
	/// Parameters are resolved from registered dependencies.
	/// Creates a fresh dependency container for each execution to ensure thread safety.
	/// </summary>
	/// <param name="method">The async method to invoke</param>
	/// <param name="additionalArgs">Additional arguments to resolve against method parameters</param>
	/// <returns>Task that returns exit code (0 for success, 1 for error)</returns>
	public async Task<int> RunAsync(Delegate method, params object[] additionalArgs)
	{
		try
		{
			var resolvedArgs = ResolveDependencies(method, additionalArgs);
			var result = method.DynamicInvoke(resolvedArgs);
			
			return await ProcessResultAsync(result);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			return 1;
		}
	}

	/// <summary>
	/// Prepares the execution by setting up the dependency container and resolving method parameters.
	/// </summary>
	/// <param name="method">The method to prepare for execution</param>
	/// <param name="additionalArgs">Additional arguments to resolve against method parameters</param>
	/// <returns>The resolved arguments for method invocation</returns>
	private object?[] ResolveDependencies(Delegate method, object[] additionalArgs)
	{
		// Create a fresh dependency container for this execution
		var dependencyContainer = new Dictionary<Type, object>();
		
		// First, copy all direct instances
		foreach (var (key, value) in _instances)
		{
			dependencyContainer[key] = value;
		}
		
		// Then resolve all factories with a proper dependency provider
		var dependencyProvider = new ExecutionDependencyProvider(dependencyContainer, _factories);
		ResolveAllFactories(dependencyProvider, dependencyContainer, additionalArgs);
		
		return ResolveMethodParameters(method, additionalArgs, dependencyContainer);
	}

	/// <summary>
	/// Processes the result of a synchronous method execution.
	/// </summary>
	/// <param name="result">The result from method execution</param>
	/// <returns>Exit code</returns>
	private static int ProcessResult(object? result)
	{
		return result switch
		{
			int intValue => intValue,
			Task<int> task => task.GetAwaiter().GetResult(),
			Task => 0, // Task without return value = success
			null => 0, // void method = success
			_ => throw new InvalidOperationException($"Method must return int, Task<int>, Task, or void. Got: {result.GetType()}")
		};
	}

	/// <summary>
	/// Processes the result of an asynchronous method execution.
	/// </summary>
	/// <param name="result">The result from method execution</param>
	/// <returns>Task that returns exit code</returns>
	private static async Task<int> ProcessResultAsync(object? result)
	{
		return result switch
		{
			int intValue => intValue,
			Task<int> task => await task,
			Task task => await ProcessTaskAsync(task),
			null => 0, // void method = success
			_ => throw new InvalidOperationException($"Method must return int, Task<int>, Task, or void. Got: {result.GetType()}")
		};
	}

	/// <summary>
	/// Helper method to process a non-generic Task.
	/// </summary>
	/// <param name="task">The task to await</param>
	/// <returns>Always returns 0 (success)</returns>
	private static async Task<int> ProcessTaskAsync(Task task)
	{
		await task;
		return 0;
	}

	/// <summary>
	/// Resolves all registered factories to ensure all dependencies are instantiated.
	/// </summary>
	private void ResolveAllFactories(IDependencyProvider provider, Dictionary<Type, object> container, object[] additionalArgs)
	{
		// Create a copy of factory entries to avoid modification during iteration
		var factoriesToResolve = _factories.ToArray();

		foreach (var kvp in factoriesToResolve)
		{
			var type = kvp.Key;
			var factory = kvp.Value;

			// Only resolve if not already instantiated
			if (!container.ContainsKey(type))
			{
				// Create an enhanced provider that can resolve from both container and additional args
				var enhancedProvider = new EnhancedExecutionDependencyProvider(container, _factories, additionalArgs);
				var instance = factory(enhancedProvider) ?? throw new InvalidOperationException($"Factory for type {type.Name} returned null");
				container[type] = instance;
			}
		}
	}

	/// <summary>
	/// Resolves method parameters by matching types from registered services and additional arguments.
	/// </summary>
	private static object?[] ResolveMethodParameters(Delegate method, object[] additionalArgs, Dictionary<Type, object> dependencyContainer)
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
			
			// First try to find matching argument by type from additional args (highest priority)
			object? matchedArg = null;
			for (int argIndex = 0; argIndex < additionalArgs.Length; argIndex++)
			{
				if (usedArgIndices.Contains(argIndex))
					continue;

				var arg = additionalArgs[argIndex];
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
				continue;
			}
			
			// Then try to resolve from the dependency container
			if (dependencyContainer.TryGetValue(parameterType, out var service))
			{
				resolvedArgs[paramIndex] = service;
				continue;
			}
			
			// Finally, use default values or throw if not possible
			if (parameter.HasDefaultValue)
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
					$"No registered service found, no matching argument found, and parameter has no default value.");
			}
		}

		return resolvedArgs;
	}

	#region Legacy
	/// <summary>
	/// Runs a method with automatic dependency injection-like parameter resolution.
	/// ILogger is automatically constructed if not provided in args.
	/// </summary>
	/// <param name="method">The method to invoke</param>
	/// <param name="args">Arguments to resolve against method parameters</param>
	/// <returns>Exit code (0 for success, 1 for error)</returns>
	[Obsolete("Use instance instead.")]
	public static int RunNew(Delegate method, params object[] args)
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
	#endregion
}
