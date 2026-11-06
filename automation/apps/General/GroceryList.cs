using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// The shopping list (<c>todo.boodschappen</c>): ticks off everything on it when "Alles gehaald" (on the list) or
/// "Alle boodschappen gedaan" (on Home) is pressed. The button on Home shows an hour after Vincent or Carleen left the supermarket
/// zone, as long as something is still open (<see cref="AskHelper"/>). Walking into the zone is handled by <see cref="TodoZoneReminders"/>.
/// </summary>
[NetDaemonApp(Id = nameof(GroceryList))]
public partial class GroceryList : BaseApp
{
    /// <summary>Pressing this button ticks off every open item of the shopping list.</summary>
    public const string DoneButton = "input_button.boodschappen_alles_gehaald";

    /// <summary>On while the button on Home should show.</summary>
    public const string AskHelper = "input_boolean.boodschappen_afvinken";

    /// <summary>How long after leaving the supermarket the button on Home shows.</summary>
    public static readonly TimeSpan ShowAfter = TimeSpan.FromHours(1);

    private static readonly string[] People = ["person.vincent_maarschalkerweerd", "person.carleen"];

    private readonly Dictionary<string, IDisposable> _waiting = [];

    public GroceryList(
        IHaContext ha,
        ILogger<GroceryList> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        new Entity(HaContext, DoneButton).StateChanges().Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
            .Subscribe(press => _ = CompleteAllAsync());

        foreach (var person in People)
        {
            var id = person;
            new Entity(HaContext, id).StateChanges().Where(x => x.Old?.State != x.New?.State).Subscribe(x => Moved(id, x.Old?.State, x.New?.State));
        }

        // Nothing left to do: the button goes away by itself
        new Entity(HaContext, TodoZoneReminders.GroceryList).StateChanges().Where(x => x.New?.State == "0")
            .Subscribe(_ => HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity(AskHelper)));
    }

    /// <summary>The words that start a WhatsApp message for the shopping list: "Boodschappen melk", "AH: melk, brood".</summary>
    [GeneratedRegex(@"^\s*(?:(?:boodschappenlijstje|boodschappenlijst|boodschappen|boodschap|albert\s*heijn|supermarkt|winkel|lijstje|ah)\b[\s,:;.\-–]*)+(?<rest>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MessageRegex();

    /// <summary>A message that is probably a chat sentence and not a list of products: a question, or very long.</summary>
    private const int MaxWordsPerItem = 6;

    /// <summary>
    /// The products in a WhatsApp message that starts with a shopping word ("Boodschappen", "AH", "Albert Heijn", "supermarkt", ...),
    /// split on commas, semicolons and new lines, or null when the message is not for the shopping list.
    /// </summary>
    public static List<string>? ParseMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains('?')) return null;
        var match = MessageRegex().Match(text);
        if (!match.Success) return null;

        var items = match.Groups["rest"].Value
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(i => i.Trim(' ', '.', '!'))
            .Where(i => i.Length > 0)
            .Select(i => char.ToUpper(i[0], System.Globalization.CultureInfo.GetCultureInfo("nl-NL")) + i[1..])
            .ToList();
        return items.Count == 0 || items.Any(i => i.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MaxWordsPerItem) ? null : items;
    }

    private static bool IsSupermarket(string? state) =>
        string.Equals(state?.Trim(), TodoZoneReminders.GroceryZone, StringComparison.OrdinalIgnoreCase);

    /// <summary>Leaving the supermarket starts the hour; going back in before it has passed stops it.</summary>
    private void Moved(string person, string? oldState, string? newState)
    {
        if (IsSupermarket(newState))
        {
            Cancel(person);
            return;
        }
        if (!IsSupermarket(oldState)) return;

        Cancel(person);
        _waiting[person] = Scheduler.Schedule(ShowAfter, () => { _ = AskAsync(person); });
    }

    private void Cancel(string person)
    {
        if (_waiting.Remove(person, out var pending)) pending.Dispose();
    }

    private async Task AskAsync(string person)
    {
        try
        {
            _waiting.Remove(person);
            if ((await OpenItemsAsync()).Count == 0) return;
            HaContext.CallService("input_boolean", "turn_on", ServiceTarget.FromEntity(AskHelper));
            Logger.LogInformation("An hour after the supermarket ({Person}): the button for the shopping list is shown", person);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not show the button for the shopping list");
        }
    }

    private async Task CompleteAllAsync()
    {
        try
        {
            var items = await OpenItemsAsync();
            foreach (var item in items)
                HaContext.CallService("todo", "update_item", ServiceTarget.FromEntity(TodoZoneReminders.GroceryList),
                    new Dictionary<string, object> { ["item"] = item, ["status"] = "completed" });
            HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity(AskHelper));
            Logger.LogInformation("Shopping list: {Count} item(s) ticked off", items.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not tick off the shopping list");
        }
    }

    /// <summary>The ids (or the names when there is no id) of the items not done yet.</summary>
    private async Task<List<string>> OpenItemsAsync()
    {
        var list = TodoZoneReminders.GroceryList;
        var response = await HaContext.CallServiceWithResponseAsync("todo", "get_items", ServiceTarget.FromEntity(list),
            new Dictionary<string, object> { ["status"] = "needs_action" });
        var items = new List<string>();
        if (response is { } root && root.TryGetProperty(list, out var entity) && entity.TryGetProperty("items", out var array))
            foreach (var item in array.EnumerateArray())
            {
                var id = item.TryGetProperty("uid", out var uid) ? uid.GetString() : null;
                var name = id is { Length: > 0 } ? id : item.GetProperty("summary").GetString();
                if (!string.IsNullOrEmpty(name)) items.Add(name);
            }
        return items;
    }
}
