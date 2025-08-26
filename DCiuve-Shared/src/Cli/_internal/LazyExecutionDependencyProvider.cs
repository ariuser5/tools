using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli;

/// <summary>
/// A lazy dependency provider that only resolves factories when services are actually requested.
/// This matches ASP.NET Core's lazy resolution behavior.
/// </summary>
internal class LazyExecutionDependencyProvider : IDependencyProvider
{
	private readonly Dictionary<Type, object> _instances;
	private readonly Dictionary<Type, Func<IDependencyProvider, object>> _factories;
	private readonly object[] _additionalArgs;
	private readonly Dictionary<Type, object> _resolvedServices = new();

	public LazyExecutionDependencyProvider(
		Dictionary<Type, object> instances,
		Dictionary<Type, Func<IDependencyProvider, object>> factories,
		object[] additionalArgs)
	{
		_instances = instances;
		_factories = factories;
		_additionalArgs = additionalArgs;
	}

	public T GetDependency<T>() where T : class
	{
		return TryGetDependency<T>(out var dependency) 
			? dependency 
			: throw new InvalidOperationException($"Service of type '{typeof(T).Name}' is not registered.");
	}

	public bool TryGetDependency<T>([NotNullWhen(true)] out T? dependency) where T : class
	{
		if (TryGetService(typeof(T), out var service) && service != null)
		{
			dependency = (T)service;
			return true;
		}
		
		dependency = null;
		return false;
	}

	public bool TryGetService(Type serviceType, out object? service)
	{
		// First check if we've already resolved this service (singleton behavior within this execution)
		if (_resolvedServices.TryGetValue(serviceType, out service))
		{
			return true;
		}

		// Then check direct instances
		if (_instances.TryGetValue(serviceType, out service))
		{
			_resolvedServices[serviceType] = service;
			return true;
		}

		// Finally, check if we have a factory for this type (LAZY RESOLUTION - only runs now!)
		if (_factories.TryGetValue(serviceType, out var factory))
		{
			try
			{
				// Create enhanced provider for factory execution
				var enhancedProvider = new EnhancedLazyDependencyProvider(_instances, _factories, _additionalArgs, _resolvedServices);
				service = factory(enhancedProvider) ?? throw new InvalidOperationException($"Factory for type {serviceType.Name} returned null");
				_resolvedServices[serviceType] = service; // Cache the result
				return true;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error creating service of type '{serviceType.Name}': {ex.Message}", ex);
			}
		}

		service = null;
		return false;
	}
}
