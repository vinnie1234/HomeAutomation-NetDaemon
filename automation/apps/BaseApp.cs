using System.Reactive.Concurrency;
using Automation.Models.Persons;

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

    internal readonly VincentModel Vincent;
    internal readonly CarleenModel Carleen;

    /// <summary>
    /// True when the house should behave in night/quiet mode.
    /// This is when Vincent is sleeping, OR when Carleen is home and sleeping.
    /// Use this for rooms near the bedroom (hall, bathroom) where Carleen's sleep matters.
    /// </summary>
    protected bool IsNightMode => Vincent.IsSleeping || (Carleen.IsHome && Carleen.IsSleeping);

    /// <summary>
    /// True only when Vincent himself is sleeping, regardless of Carleen.
    /// Use this for living spaces (e.g. the living room) that should stay active when Vincent is
    /// awake even if Carleen is still asleep in the bedroom.
    /// </summary>
    protected bool IsVincentNightMode => Vincent.IsSleeping;



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

        Vincent = new VincentModel(Entities);
        Carleen = new CarleenModel(Entities);

    }
    
    protected async Task ExecuteWithFallbackAsync(Func<Task> operation, Func<Task> fallback, string operationName)
    {
        try
        {
            await operation();
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