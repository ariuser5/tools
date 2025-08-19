using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli;

/// <summary>
/// Enhanced dependency provider used during execution that can resolve dependencies from both
/// the container and additional arguments passed to the Run method.
/// </summary>
internal class EnhancedExecutionDependencyProvider : IDependencyProvider
{
	private readonly Dictionary<Type, object> _container;
	private readonly Dictionary<Type, Func<IDependencyProvider, object>> _factories;
	private readonly object[] _additionalArgs;
	
	public EnhancedExecutionDependencyProvider(Dictionary<Type, object> container, Dictionary<Type, Func<IDependencyProvider, object>> factories, object[] additionalArgs)
	{
		_container = container;
		_factories = factories;
		_additionalArgs = additionalArgs;
	}
	
	public T GetDependency<T>() where T : class
	{
		// First check additional args (highest priority)
		foreach (var arg in _additionalArgs)
		{
			if (arg is T typedArg)
				return typedArg;
		}
		
		// Then check if already instantiated in container
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
		
		throw new InvalidOperationException($"Dependency of type '{typeof(T).Name}' is not registered and not found in additional arguments.");
	}

	public bool TryGetDependency<T>([NotNullWhen(true)] out T? dependency) where T : class
	{
		// First check additional args (highest priority)
		foreach (var arg in _additionalArgs)
		{
			if (arg is T typedArg)
			{
				dependency = typedArg;
				return true;
			}
		}
		
		// Then check if already instantiated in container
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
