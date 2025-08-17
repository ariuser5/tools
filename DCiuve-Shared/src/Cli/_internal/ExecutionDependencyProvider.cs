using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli;

/// <summary>
/// Internal dependency provider used during execution to provide isolated dependency resolution.
/// </summary>
internal class ExecutionDependencyProvider : IDependencyProvider
{
	private readonly Dictionary<Type, object> _container;
	private readonly Dictionary<Type, Func<IDependencyProvider, object>> _factories;
	
	public ExecutionDependencyProvider(Dictionary<Type, object> container, Dictionary<Type, Func<IDependencyProvider, object>> factories)
	{
		_container = container;
		_factories = factories;
	}
	
	public T GetDependency<T>() where T : class
	{
		// First check if already instantiated
		if (_container.TryGetValue(typeof(T), out var service))
			return (T)service;
			
		// Then check if there's a factory for it
		if (_factories.TryGetValue(typeof(T), out var factory))
		{
			var instance = factory(this) ?? throw new InvalidOperationException($"Factory for type {typeof(T).Name} returned null");

			// Cache the instance for future requests within this execution
			_container[typeof(T)] = instance;
			return (T)instance;
		}
		
		throw new InvalidOperationException($"Dependency of type '{typeof(T).Name}' is not registered.");
	}

	public bool TryGetDependency<T>([NotNullWhen(true)]out T? dependency) where T : class
	{
		// First check if already instantiated
		if (_container.TryGetValue(typeof(T), out var service))
		{
			dependency = (T)service;
			return true;
		}
			
		// Then check if there's a factory for it
		if (_factories.TryGetValue(typeof(T), out var factory))
		{
			try
			{
				var instance = factory(this);
				if (instance == null)
				{
					dependency = null;
					return false;
				}
					
				// Cache the instance for future requests within this execution
				_container[typeof(T)] = instance;
				dependency = (T)instance;
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