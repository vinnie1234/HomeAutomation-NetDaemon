using System.Reactive.Concurrency;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;
using Automation.Models.Todo;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(TodoManager))]
public class TodoManager : BaseApp
{
    private readonly string _discordTodoChannel = ConfigManager.GetValueFromConfigNested("Discord", "TODO") ?? "";
    
    public TodoManager(IHaContext haContext,  ILogger<TodoManager> logger, INotify notify, IScheduler scheduler) 
        : base(haContext, logger, notify, scheduler)
    {
        Scheduler.ScheduleCron("00 22 * * *", HandleTodoList);
    }

    private void HandleTodoList()
    {
       var notCompleteList = new List<Item>();

        var response = Entities.Todo.Dagelijks.GetItemsAsync().Result;
        var responseModel = response?.Deserialize<TodoModel>();
        if (responseModel?.TodoDagelijks.Items != null)
            foreach (var todoDagelijksItem in responseModel.TodoDagelijks.Items)
            {
                if (todoDagelijksItem.Status == "needs_action")
                {
                    notCompleteList.Add(todoDagelijksItem);  
                }
                
                Entities.Todo.Dagelijks.RemoveItem(todoDagelijksItem.Uid);
            }

        if (notCompleteList.Count != 0)
        {
            var fieldList = (from item in notCompleteList where item.Status == "needs_action" select new Field { Name = "Niet gedaan", Value = item.Summary }).ToList();
            
            Notify.NotifyDiscord("", [_discordTodoChannel], new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Title = "Niet alles gedaan wat je moest doen!",
                    Url = ConfigManager.GetValueFromConfig("BaseUrlHomeAssistant") + "/todo?entity_id=todo.dagelijks",
                    Fields = fieldList.ToArray()
                }
            });
        }

        var todoList = GetTodoItemsToday();

        foreach (var todoAddItemParameters in todoList)
        {
            Entities.Todo.Dagelijks.AddItem(todoAddItemParameters);
        }
        

    }

    private static List<TodoAddItemParameters> GetTodoItemsToday()
    {
        var todoList = new List<TodoAddItemParameters>
        {
            new()
            {
                Item = "Login Xbox App",
            },
            new()
            {
                Item = "Microsoft Rewards punten scoren",
            }
        };
        return todoList;
    }
}
