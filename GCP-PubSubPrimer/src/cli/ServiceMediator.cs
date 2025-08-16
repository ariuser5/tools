using DCiuve.Shared.Logging;

namespace DCiuve.Gcp.PubSub.Cli;

/// <summary>
/// Enhanced mediator that uses abstract factory pattern for authenticated service strategies.
/// Handles the complete flow: scope normalization → authentication → strategy creation.
/// </summary>
public class ServiceMediator(
	IServiceStrategyAbstractFactory abstractFactory,
	ILogger logger)
{
	/// <summary>
	/// Executes a watch operation using the appropriate service strategy.
	/// </summary>
	public async Task ExecuteWatchAsync(
		string serviceName,
		Stream clientSecretStream,
		string applicationName,
		string projectId,
		string topicId,
		string[]? customScopes,
		int? frequency,
		ForceNewMode forceNewMode,
		CancellationToken cancellationToken = default)
	{
		// Create authenticated strategy using the abstract factory
		// This handles: scope normalization → authentication → strategy creation with authenticated service
		var strategy = await abstractFactory.CreateStrategyAsync(
			serviceName, clientSecretStream, applicationName, customScopes, cancellationToken);

		if (!frequency.HasValue)
		{
			// Single execution
			logger.Info("Running once...");
			var forceNew = forceNewMode != ForceNewMode.False;

			logger.Info("Executing {0} watch request (single execution, forceNew: {1}) at {2:yyyy-MM-dd HH:mm:ss}",
				serviceName, forceNew, DateTime.Now);
			
			var result = await strategy.ExecuteWatchAsync(projectId, topicId, forceNew, cancellationToken);
			
			LogWatchResult(result, serviceName);
			return;
		}

		// Repeated execution
		logger.Info("Running {0} watch with repeat frequency: {1} minutes", serviceName, frequency.Value);
		await ExecuteRepeatedAsync(strategy, applicationName, projectId, topicId, forceNewMode, frequency.Value, cancellationToken);
	}

	/// <summary>
	/// Cancels a watch operation using the appropriate service strategy.
	/// </summary>
	public async Task<bool> CancelWatchAsync(
		string serviceName,
		Stream clientSecretStream,
		string applicationName,
		CancellationToken cancellationToken = default)
	{
		// Create authenticated strategy using the abstract factory
		var strategy = await abstractFactory.CreateStrategyAsync(
			serviceName, clientSecretStream, applicationName, null, cancellationToken);

		return await strategy.StopWatchAsync(cancellationToken);
	}

	private async Task ExecuteRepeatedAsync(
		IServiceStrategy strategy,
		string serviceName,
		string projectId,
		string topicId,
		ForceNewMode forceNewMode,
		int frequencyMinutes,
		CancellationToken cancellationToken)
	{
		// Track execution count for "First" mode
		var executionCount = 0;

		// Schedule the task to run at specified frequency
		var timer = new Timer(
			callback: async _ =>
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				var isFirstExecution = Interlocked.Increment(ref executionCount) == 1;

				// Calculate forceNew for this execution
				var forceNew = forceNewMode switch
				{
					ForceNewMode.False => false,
					ForceNewMode.First => isFirstExecution,
					ForceNewMode.Always => true,
					_ => throw new ArgumentOutOfRangeException(nameof(forceNewMode), forceNewMode, "Invalid ForceNewMode value")
				};

				logger.Info("Executing {0} watch request (execution #{1}, forceNew: {2}) at {3:yyyy-MM-dd HH:mm:ss}",
					serviceName, executionCount, forceNew, DateTime.Now);

				try
				{
					var result = await strategy.ExecuteWatchAsync(projectId, topicId, forceNew, cancellationToken);
					LogWatchResult(result, serviceName);

					// Check if next execution will happen before watch expiration
					CheckNextExecutionTiming(result, frequencyMinutes, serviceName);
				}
				catch (Exception ex)
				{
					logger.Error("{0} watch execution failed: {1}", serviceName, ex.Message);
					logger.Debug("{0} watch execution exception details: {1}", serviceName, ex);
				}
			},
			state: null,
			dueTime: TimeSpan.Zero,
			period: TimeSpan.FromMinutes(frequencyMinutes));

		// Register cleanup when cancellation is requested
		using var registration = cancellationToken.Register(() =>
		{
			logger.Debug("Cancellation requested, disposing timer...");
			timer.Dispose();
		});

		// Wait for cancellation
		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
		}
		catch (OperationCanceledException) { }
		
		logger.Info("Application stopped. Note: {0} watch remains active on Google's servers until expiration.", serviceName);
	}

	private void LogWatchResult(IWatchResult result, string serviceName)
	{
		// Log expiration information if available
		if (result.Expiration.HasValue)
		{
			var timeUntilExpiration = result.Expiration.Value - DateTime.UtcNow;
			logger.Info("{0} watch expires at {1:yyyy-MM-dd HH:mm:ss} UTC (in {2:d\\.hh\\:mm\\:ss})",
				serviceName, result.Expiration.Value, timeUntilExpiration);
		}
	}

	private void CheckNextExecutionTiming(IWatchResult watchResult, int frequencyMinutes, string serviceName)
	{
		if (!watchResult.Expiration.HasValue)
			return;

		var nextExecution = DateTime.Now.AddMinutes(frequencyMinutes);
		var watchExpiration = watchResult.Expiration.Value;

		if (nextExecution > watchExpiration)
		{
			var timeUntilExpiration = watchExpiration - DateTime.UtcNow;
			var timeUntilNextExecution = TimeSpan.FromMinutes(frequencyMinutes);

			logger.Warning("Next {0} execution scheduled at {1:yyyy-MM-dd HH:mm:ss} will be AFTER watch expiration at {2:yyyy-MM-dd HH:mm:ss}!",
				serviceName, nextExecution, watchExpiration);
			logger.Warning("{0} watch expires in {1:d\\.hh\\:mm\\:ss}, but next execution is in {2:d\\.hh\\:mm\\:ss}",
				serviceName, timeUntilExpiration, timeUntilNextExecution);
		}
		else
		{
			logger.Debug("Next {0} execution at {1:yyyy-MM-dd HH:mm:ss} will be before watch expiration at {2:yyyy-MM-dd HH:mm:ss}",
				serviceName, nextExecution, watchExpiration);
		}
	}
}
