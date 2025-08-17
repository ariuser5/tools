namespace DCiuve.Shared.Pipeline;

/// <summary>
/// Represents a pipeline step that can be chained with other operations.
/// Similar to RxJS Observable but for synchronous operations.
/// </summary>
/// <typeparam name="T">The type of data flowing through the pipeline</typeparam>
public class ExecutionFlow<T>
{
	private readonly Func<T> _source;

	internal ExecutionFlow(Func<T> source)
	{
		_source = source ?? throw new ArgumentNullException(nameof(source));
	}

	/// <summary>
	/// Executes the pipeline and returns the result
	/// </summary>
	public T Execute() => _source();

	/// <summary>
	/// Transforms each element using a selector function (similar to RxJS map)
	/// </summary>
	public ExecutionFlow<TResult> Map<TResult>(Func<T, TResult> selector)
	{
		return new ExecutionFlow<TResult>(() => selector(_source()));
	}

	/// <summary>
	/// Adds additional context to the pipeline (similar to RxJS withLatestFrom)
	/// </summary>
	public ExecutionFlow<(T, TIn)> Use<TIn>(Func<TIn> factory)
	{
		return new ExecutionFlow<(T, TIn)>(() =>
		{
			var data = _source();
			var additionalData = factory();
			return (data, additionalData);
		});
	}

	/// <summary>
	/// Filters elements based on a predicate (similar to RxJS filter)
	/// </summary>
	public ExecutionFlow<T> Filter(Func<T, bool> predicate)
	{
		return new ExecutionFlow<T>(() =>
		{
			var value = _source();
			return predicate(value) ? value : throw new InvalidOperationException("Value filtered out");
		});
	}

	/// <summary>
	/// Performs a side effect without modifying the value (similar to RxJS tap)
	/// </summary>
	public ExecutionFlow<T> Tap(Action<T> action)
	{
		return new ExecutionFlow<T>(() =>
		{
			var value = _source();
			action(value);
			return value;
		});
	}

	/// <summary>
	/// Catches exceptions and provides a fallback value (similar to RxJS catchError)
	/// </summary>
	public ExecutionFlow<T> Catch(Func<Exception, T> errorHandler)
	{
		return new ExecutionFlow<T>(() =>
		{
			try
			{
				return _source();
			}
			catch (Exception ex)
			{
				return errorHandler(ex);
			}
		});
	}

	/// <summary>
	/// Catches exceptions and provides a fallback value
	/// </summary>
	public ExecutionFlow<T> CatchWith(T fallbackValue)
	{
		return Catch(_ => fallbackValue);
	}

	/// <summary>
	/// Flattens nested ExecutionFlow (similar to RxJS flatMap/mergeMap)
	/// </summary>
	public ExecutionFlow<TResult> FlatMap<TResult>(Func<T, ExecutionFlow<TResult>> selector)
	{
		return new ExecutionFlow<TResult>(() => selector(_source()).Execute());
	}

	/// <summary>
	/// Switches to another ExecutionFlow based on a condition (similar to RxJS switchMap)
	/// </summary>
	public ExecutionFlow<TResult> Switch<TResult>(
		Func<T, bool> predicate, 
		Func<T, ExecutionFlow<TResult>> trueCase,
		Func<T, ExecutionFlow<TResult>> falseCase)
	{
		return new ExecutionFlow<TResult>(() =>
		{
			var value = _source();
			var selectedFlow = predicate(value) ? trueCase(value) : falseCase(value);
			return selectedFlow.Execute();
		});
	}

	/// <summary>
	/// Validates the value and throws if validation fails
	/// </summary>
	public ExecutionFlow<T> Validate(Func<T, bool> validator, string errorMessage = "Validation failed")
	{
		return new ExecutionFlow<T>(() =>
		{
			var value = _source();
			return validator(value) ? value : throw new InvalidOperationException(errorMessage);
		});
	}

	/// <summary>
	/// Executes an async operation and waits for result
	/// </summary>
	public ExecutionFlow<TResult> MapAsync<TResult>(Func<T, Task<TResult>> asyncSelector)
	{
		return new ExecutionFlow<TResult>(() => asyncSelector(_source()).GetAwaiter().GetResult());
	}

	/// <summary>
	/// Applies a transformation only if the value is not null
	/// </summary>
	public ExecutionFlow<TResult?> MapIfNotNull<TResult>(Func<T, TResult> selector) where TResult : class
	{
		return new ExecutionFlow<TResult?>(() =>
		{
			var value = _source();
			return value != null ? selector(value) : null;
		});
	}

	/// <summary>
	/// Retries the operation a specified number of times on failure
	/// </summary>
	public ExecutionFlow<T> Retry(int maxAttempts, TimeSpan? delay = null)
	{
		return new ExecutionFlow<T>(() =>
		{
			Exception? lastException = null;
			for (int attempt = 0; attempt < maxAttempts; attempt++)
			{
				try
				{
					return _source();
				}
				catch (Exception ex)
				{
					lastException = ex;
					if (attempt < maxAttempts - 1 && delay.HasValue)
					{
						Thread.Sleep(delay.Value);
					}
				}
			}
			throw lastException!;
		});
	}
}

/// <summary>
/// Static factory methods for creating ExecutionFlow pipelines
/// </summary>
public static class ExecutionFlow
{
	/// <summary>
	/// Creates an ExecutionFlow from a value
	/// </summary>
	public static ExecutionFlow<T> From<T>(T value)
	{
		return new ExecutionFlow<T>(() => value);
	}

	/// <summary>
	/// Creates an ExecutionFlow from a function
	/// </summary>
	public static ExecutionFlow<T> From<T>(Func<T> factory)
	{
		return new ExecutionFlow<T>(factory);
	}

	/// <summary>
	/// Creates an ExecutionFlow from an async operation
	/// </summary>
	public static ExecutionFlow<T> FromAsync<T>(Func<Task<T>> asyncFactory)
	{
		return new ExecutionFlow<T>(() => asyncFactory().GetAwaiter().GetResult());
	}

	/// <summary>
	/// Creates an empty ExecutionFlow (useful for starting chains)
	/// </summary>
	public static ExecutionFlow<T> Empty<T>() where T : new()
	{
		return new ExecutionFlow<T>(() => new T());
	}

	/// <summary>
	/// Creates an ExecutionFlow that throws an exception
	/// </summary>
	public static ExecutionFlow<T> Error<T>(Exception exception)
	{
		return new ExecutionFlow<T>(() => throw exception);
	}

	/// <summary>
	/// Combines multiple ExecutionFlows (similar to RxJS combineLatest)
	/// </summary>
	public static ExecutionFlow<(T1, T2)> Combine<T1, T2>(
		ExecutionFlow<T1> flow1, 
		ExecutionFlow<T2> flow2)
	{
		return new ExecutionFlow<(T1, T2)>(() => (flow1.Execute(), flow2.Execute()));
	}

	/// <summary>
	/// Combines three ExecutionFlows
	/// </summary>
	public static ExecutionFlow<(T1, T2, T3)> Combine<T1, T2, T3>(
		ExecutionFlow<T1> flow1, 
		ExecutionFlow<T2> flow2, 
		ExecutionFlow<T3> flow3)
	{
		return new ExecutionFlow<(T1, T2, T3)>(() => (flow1.Execute(), flow2.Execute(), flow3.Execute()));
	}

	/// <summary>
	/// Creates a conditional ExecutionFlow
	/// </summary>
	public static ExecutionFlow<T> If<T>(bool condition, Func<ExecutionFlow<T>> trueCase, Func<ExecutionFlow<T>> falseCase)
	{
		return new ExecutionFlow<T>(() => (condition ? trueCase() : falseCase()).Execute());
	}
}