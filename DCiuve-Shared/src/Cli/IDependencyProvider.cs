using System.Diagnostics.CodeAnalysis;

namespace DCiuve.Shared.Cli;

/// <summary>
/// Simple dependency provider interface for dependency resolution.
/// </summary>
public interface IDependencyProvider
{
	/// <summary>
	/// Gets a registered dependency of the specified type.
	/// Throws an exception if the dependency is not found.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <returns>The dependency instance</returns>
	/// <exception cref="InvalidOperationException">Thrown when the dependency is not registered</exception>
	T GetDependency<T>() where T : class;

	/// <summary>
	/// Tries to get a registered dependency of the specified type.
	/// </summary>
	/// <typeparam name="T">The dependency type</typeparam>
	/// <param name="dependency">The retrieved dependency, or null if not found</param>
	/// <returns>True if the dependency was found, false otherwise</returns>
	bool TryGetDependency<T>([NotNullWhen(true)] out T? dependency) where T : class;
}

/// <summary>
/// Generic dependency provider interface for typed dependency resolution during factory registration.
/// </summary>
/// <typeparam name="T">The type being registered</typeparam>
public interface IDependencyProvider<T> : IDependencyProvider where T : class
{
	/// <summary>
	/// Gets a previously registered dependency that is being overwritten.
	/// This is only available during factory registration and allows for decorator patterns.
	/// Returns the same type that is currently being registered.
	/// </summary>
	/// <returns>The previously registered dependency instance, or null if none was registered</returns>
	T? GetPrevious();
}
