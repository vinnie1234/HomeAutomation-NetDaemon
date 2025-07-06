using System.Net.Http;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using Polly;
using Polly.CircuitBreaker;

namespace Automation.apps;

/// <summary>
/// Represents the base application class that provides common functionality for all derived applications.
/// </summary>
public class BaseApp
{
    /// <summary>
    /// Gets the entities available in the Home Assistant context.
    /// </summary>
    internal readonly Entities Entities;

    /// <summary>
    /// Gets the logger instance for logging messages.
    /// </summary>
    internal readonly ILogger Logger;

    /// <summary>
    /// Gets the notification service for sending notifications.
    /// </summary>
    internal readonly INotify Notify;

    /// <summary>
    /// Gets the scheduler for scheduling tasks.
    /// </summary>
    internal readonly IScheduler Scheduler;

    /// <summary>
    /// Gets the services available in the Home Assistant context.
    /// </summary>
    internal readonly IServices Services;

    /// <summary>
    /// Gets the Home Assistant context.
    /// </summary>
    internal readonly IHaContext HaContext;

    internal readonly PersonModel Vincent;

    /// <summary>
    /// Gets the resilience pipeline for handling failures.
    /// </summary>
    protected readonly ResiliencePipeline ResiliencePipeline;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseApp"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for scheduling tasks.</param>
    protected BaseApp(
        IHaContext haContext,
        ILogger logger,
        INotify notify,
        IScheduler scheduler)
    {
        HaContext = haContext;
        Logger = logger;
        Notify = notify;
        Scheduler = scheduler;
        Entities = new Entities(haContext);
        Services = new Services(haContext);

        Vincent = new PersonModel(Entities);

        // Initialize resilience pipeline with Polly v8
        ResiliencePipeline = CreateResiliencePipeline();

        Logger.LogDebug("Started {Name} with resilience pipeline", GetType().Name);
    }

    /// <summary>
    /// Creates a resilience pipeline for handling transient failures.
    /// </summary>
    /// <returns>The configured resilience pipeline.</returns>
    private ResiliencePipeline CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>()
                    .Handle<SocketException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    Logger.LogWarning("Retry {AttemptNumber} for operation after {Delay}ms due to: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown error");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromMinutes(2),
                OnOpened = args =>
                {
                    Logger.LogWarning("Circuit breaker opened for {Duration} due to: {Exception}",
                        args.BreakDuration, args.Outcome.Exception?.Message ?? "Unknown error");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    Logger.LogInformation("Circuit breaker closed - normal operation resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    Logger.LogInformation("Circuit breaker half-open - testing if service recovered");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Executes an operation with resilience patterns (retry + circuit breaker).
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation for logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task ExecuteWithResilienceAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await ResiliencePipeline.ExecuteAsync(async _ => await operation());
            Logger.LogDebug("Operation {Operation} completed successfully", operationName);
        }
        catch (BrokenCircuitException)
        {
            Logger.LogWarning("Operation {Operation} blocked by circuit breaker", operationName);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Operation {Operation} failed after all retry attempts", operationName);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation with resilience patterns and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation for logging.</param>
    /// <returns>A task representing the asynchronous operation with result.</returns>
    protected async Task<T> ExecuteWithResilienceAsync<T>(Func<Task<T>> operation, string operationName)
    {
        try
        {
            var result = await ResiliencePipeline.ExecuteAsync(async _ => await operation());
            Logger.LogDebug("Operation {Operation} completed successfully", operationName);
            return result;
        }
        catch (BrokenCircuitException)
        {
            Logger.LogWarning("Operation {Operation} blocked by circuit breaker", operationName);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Operation {Operation} failed after all retry attempts", operationName);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation with resilience patterns and fallback mechanism.
    /// </summary>
    /// <param name="operation">The primary operation to execute.</param>
    /// <param name="fallback">The fallback operation if primary fails.</param>
    /// <param name="operationName">The name of the operation for logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task ExecuteWithFallbackAsync(Func<Task> operation, Func<Task> fallback, string operationName)
    {
        try
        {
            await ExecuteWithResilienceAsync(operation, operationName);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Primary operation {Operation} failed, executing fallback", operationName);
            try
            {
                await fallback();
                Logger.LogInformation("Fallback for operation {Operation} executed successfully", operationName);
            }
            catch (Exception fallbackEx)
            {
                Logger.LogError(fallbackEx, "Fallback for operation {Operation} also failed", operationName);
                throw new AggregateException("Both primary and fallback operations failed", ex, fallbackEx);
            }
        }
    }
}