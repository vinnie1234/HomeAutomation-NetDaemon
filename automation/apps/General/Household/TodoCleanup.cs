using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Every night at midnight (Dutch time) removes the completed items from all todo lists in Home Assistant, so the lists
/// only show what still has to be done.
/// </summary>
[NetDaemonApp(Id = nameof(TodoCleanup))]
public class TodoCleanup : BaseApp
{
    public TodoCleanup(
        IHaContext ha,
        ILogger<TodoCleanup> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        Scheduler.ScheduleCronNl("0 0 * * *", RemoveCompleted);
    }

    /// <summary>Removes the completed items of every todo list; a list that fails does not stop the others.</summary>
    private void RemoveCompleted()
    {
        foreach (var list in Entities.Todo.EnumerateAll())
        {
            try
            {
                new TodoEntity(HaContext, list.EntityId).RemoveCompletedItems();
                Logger.LogInformation("Removed the completed items of {List}", list.EntityId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not remove the completed items of {List}", list.EntityId);
            }
        }
    }
}
