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

    }
    
    protected void ExecuteWithFallbackAsync(Func<Task> operation, Func<Task> fallback, string operationName)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Primary operation {Operation} failed, executing fallback", operationName);
            try
            {
                fallback();
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