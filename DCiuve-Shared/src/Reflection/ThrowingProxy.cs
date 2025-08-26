using System.Reflection;

namespace DCiuve.Shared.Reflection;
public class ThrowingProxy<T> : DispatchProxy where T : class
{
	private Func<object>? _throwingCallback;
	private HashSet<string> _allowedMethods = new();

	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
	{
		// Allow certain methods to succeed without throwing
		if (targetMethod != null && _allowedMethods.Contains(targetMethod.Name))
		{
			// Return appropriate default value based on return type
			if (targetMethod.ReturnType == typeof(void))
				return null;
			
			if (targetMethod.ReturnType.IsValueType)
				return Activator.CreateInstance(targetMethod.ReturnType);
				
			return null; // For reference types, return null
		}

		return _throwingCallback?.Invoke();
	}

	public static T Create(Func<object> throwingCallback, params string[] allowedMethods)
	{
		var proxy = Create<T, ThrowingProxy<T>>() as ThrowingProxy<T>;
		proxy!._throwingCallback = throwingCallback;
		proxy._allowedMethods = new HashSet<string>(allowedMethods) { "Dispose" };
		return (T)(object)proxy;
	}
}