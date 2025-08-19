using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli.Runtime;

/// <summary>
/// Extension methods for runtime type operations on dependency providers.
/// These methods allow registration and retrieval of dependencies when the type is not known at compile time.
/// </summary>
public static class DependencyProviderExtensions
{
	/// <summary>
	/// Registers a dependency instance for the specified runtime type.
	/// </summary>
	/// <param name="app">The application instance</param>
	/// <param name="type">The dependency type</param>
	/// <param name="instance">The dependency instance</param>
	/// <returns>The Application instance for method chaining</returns>
	/// <exception cref="ArgumentNullException">Thrown when type or instance is null</exception>
	/// <exception cref="ArgumentException">Thrown when instance is not assignable to the specified type</exception>
	public static Application RegisterDependency(this Application app, Type type, object instance)
	{
		if (type == null)
			throw new ArgumentNullException(nameof(type));
		if (instance == null)
			throw new ArgumentNullException(nameof(instance));
		if (!type.IsAssignableFrom(instance.GetType()))
			throw new ArgumentException($"Instance of type {instance.GetType().Name} is not assignable to {type.Name}", nameof(instance));
			
		// Use reflection to call the private method that manages the instances dictionary
		var instancesField = typeof(Application).GetField("_instances", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (instancesField?.GetValue(app) is Dictionary<Type, object> instances)
		{
			instances[type] = instance;
		}
		else
		{
			throw new InvalidOperationException("Unable to access internal instances dictionary");
		}
		
		return app;
	}

	/// <summary>
	/// Registers a dependency factory for the specified runtime type.
	/// </summary>
	/// <param name="app">The application instance</param>
	/// <param name="type">The dependency type</param>
	/// <param name="factory">Factory function to create the dependency</param>
	/// <returns>The Application instance for method chaining</returns>
	/// <exception cref="ArgumentNullException">Thrown when type or factory is null</exception>
	public static Application RegisterDependency(this Application app, Type type, Func<IDependencyProvider, object> factory)
	{
		if (type == null)
			throw new ArgumentNullException(nameof(type));
		if (factory == null)
			throw new ArgumentNullException(nameof(factory));
			
		// Use reflection to call the private method that manages the factories dictionary
		var factoriesField = typeof(Application).GetField("_factories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (factoriesField?.GetValue(app) is Dictionary<Type, Func<IDependencyProvider, object>> factories)
		{
			factories[type] = factory;
		}
		else
		{
			throw new InvalidOperationException("Unable to access internal factories dictionary");
		}
		
		return app;
	}

	/// <summary>
	/// Gets a registered dependency of the specified runtime type.
	/// Throws an exception if the dependency is not found.
	/// </summary>
	/// <param name="provider">The dependency provider</param>
	/// <param name="type">The dependency type</param>
	/// <returns>The dependency instance</returns>
	/// <exception cref="ArgumentNullException">Thrown when type is null</exception>
	/// <exception cref="InvalidOperationException">Thrown when the dependency is not registered</exception>
	public static object GetDependency(this IDependencyProvider provider, Type type)
	{
		if (type == null)
			throw new ArgumentNullException(nameof(type));
			
		// Use reflection to call the generic method
		var method = typeof(IDependencyProvider).GetMethod(nameof(IDependencyProvider.GetDependency));
		if (method == null)
			throw new InvalidOperationException("GetDependency method not found");
			
		var genericMethod = method.MakeGenericMethod(type);
		var result = genericMethod.Invoke(provider, null);
		
		return result ?? throw new InvalidOperationException($"Dependency of type '{type.Name}' returned null");
	}

	/// <summary>
	/// Tries to get a registered dependency of the specified runtime type.
	/// </summary>
	/// <param name="provider">The dependency provider</param>
	/// <param name="type">The dependency type</param>
	/// <param name="dependency">The retrieved dependency, or null if not found</param>
	/// <returns>True if the dependency was found, false otherwise</returns>
	/// <exception cref="ArgumentNullException">Thrown when type is null</exception>
	public static bool TryGetDependency(this IDependencyProvider provider, Type type, [NotNullWhen(true)] out object? dependency)
	{
		if (type == null)
			throw new ArgumentNullException(nameof(type));
			
		try
		{
			// Use reflection to call the generic method
			var method = typeof(IDependencyProvider).GetMethod(nameof(IDependencyProvider.TryGetDependency));
			if (method == null)
			{
				dependency = null;
				return false;
			}
				
			var genericMethod = method.MakeGenericMethod(type);
			var parameters = new object?[] { null };
			var result = (bool?)genericMethod.Invoke(provider, parameters);
			
			dependency = parameters[0];
			return result == true && dependency != null;
		}
		catch
		{
			dependency = null;
			return false;
		}
	}
}
