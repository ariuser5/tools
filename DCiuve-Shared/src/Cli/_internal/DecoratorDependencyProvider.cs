using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli;

/// <summary>
/// Internal dependency provider used during factory registration to provide access to previously registered dependencies.
/// This enables decorator patterns where new registrations can wrap existing ones.
/// </summary>
internal class DecoratorDependencyProvider<T> : IDependencyProvider<T> where T : class
{
	private readonly IDependencyProvider _baseProvider;
	private readonly object? _previousInstance;
	private readonly Func<IDependencyProvider, object>? _previousFactory;
	
	public DecoratorDependencyProvider(IDependencyProvider baseProvider, object? previousInstance, Func<IDependencyProvider, object>? previousFactory)
	{
		_baseProvider = baseProvider;
		_previousInstance = previousInstance;
		_previousFactory = previousFactory;
	}
	
	public TDep GetDependency<TDep>() where TDep : class
	{
		return _baseProvider.GetDependency<TDep>();
	}

	public bool TryGetDependency<TDep>([NotNullWhen(true)] out TDep? dependency) where TDep : class
	{
		return _baseProvider.TryGetDependency<TDep>(out dependency);
	}

	public T? GetPrevious()
	{
		// If there was a direct instance, return it
		if (_previousInstance is T directInstance)
			return directInstance;
			
		// If there was a factory, execute it to get the instance
		if (_previousFactory != null)
		{
			var instance = _previousFactory(_baseProvider);
			return instance as T;
		}
		
		return null;
	}
}
