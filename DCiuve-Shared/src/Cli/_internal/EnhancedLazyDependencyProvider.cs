using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli;

/// <summary>
/// Enhanced lazy dependency provider that supports factory resolution with access to additional arguments.
/// </summary>
internal class EnhancedLazyDependencyProvider : IDependencyProvider
{
	private readonly Dictionary<Type, object> _instances;
	private readonly Dictionary<Type, Func<IDependencyProvider, object>> _factories;
	private readonly object[] _additionalArgs;
	private readonly Dictionary<Type, object> _resolvedServices;

	public EnhancedLazyDependencyProvider(
		Dictionary<Type, object> instances,
		Dictionary<Type, Func<IDependencyProvider, object>> factories,
		object[] additionalArgs,
		Dictionary<Type, object> resolvedServices)
	{
		_instances = instances;
		_factories = factories;
		_additionalArgs = additionalArgs;
		_resolvedServices = resolvedServices;
	}

	public T GetDependency<T>() where T : class
	{
		return TryGetDependency<T>(out var dependency) 
			? dependency 
			: throw new InvalidOperationException($"Service of type '{typeof(T).Name}' is not registered.");
	}

	public bool TryGetDependency<T>([NotNullWhen(true)] out T? dependency) where T : class
	{
		// First check resolved services
		if (_resolvedServices.TryGetValue(typeof(T), out var resolvedService))
		{
			dependency = (T)resolvedService;
			return true;
		}

		// Then check direct instances
		if (_instances.TryGetValue(typeof(T), out var instance))
		{
			dependency = (T)instance;
			return true;
		}

		// Then check additional args
		foreach (var arg in _additionalArgs)
		{
			if (arg is T typedArg)
			{
				dependency = typedArg;
				return true;
			}
		}

		// Finally check if we can create from factory
		if (_factories.TryGetValue(typeof(T), out var factory))
		{
			try
			{
				var service = factory(this);
				dependency = (T)service;
				_resolvedServices[typeof(T)] = service; // Cache for future use
				return true;
			}
			catch
			{
				dependency = null;
				return false;
			}
		}

		dependency = null;
		return false;
	}
}
