using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Todo items with a place. A task gets the line <c>📍 Boodschappen AH</c> in its description (shown under the task in the
/// list), plus <c>🔔</c> when a WhatsApp message should be sent: when that person walks into the zone, they get a message
/// with what still has to be done there. The tasks come from their own list (<c>todo.vincent</c> or <c>todo.carleen</c>) and
/// from the shared list <c>todo.dagelijks</c>.
/// <para>
/// The tasks are added from the dashboard pop-up #taak, which only fills in the helpers <c>input_select.todo_lijst</c>,
/// <c>input_text.todo_tekst</c>, <c>input_select.todo_zone</c> and <c>input_boolean.todo_stuur_bericht</c> and presses
/// <c>input_button.todo_toevoegen</c>. This app turns that into <c>todo.add_item</c> with the description above, and keeps the
/// zone choices in sync with the zones in Home Assistant.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(TodoZoneReminders))]
public partial class TodoZoneReminders : BaseApp
{
    public const string NoZone = "Geen plek";
    public const string Pin = "📍";
    public const string Bell = "🔔";

    public const string ListHelper = "input_select.todo_lijst";
    public const string TextHelper = "input_text.todo_tekst";
    public const string ZoneHelper = "input_select.todo_zone";
    public const string MessageHelper = "input_boolean.todo_stuur_bericht";
    public const string AddButton = "input_button.todo_toevoegen";

    private const string SharedList = "todo.dagelijks";
    private const string HomeZoneName = "Thuis";

    /// <summary>How long to wait after the button before reading the form.</summary>
    private static readonly TimeSpan ButtonDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>The GPS wanders around the edge of a zone: entering it again within this time is not announced again.</summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(30);

    /// <summary>The list names of the form and the todo list each one stands for.</summary>
    private static readonly IReadOnlyDictionary<string, string> Lists = new Dictionary<string, string>
    {
        ["Vincent"] = "todo.vincent",
        ["Carleen"] = "todo.carleen",
        ["Samen"] = SharedList
    };

    private readonly Dictionary<(string Person, string Zone), DateTimeOffset> _lastReminder = [];

    public TodoZoneReminders(
        IHaContext ha,
        ILogger<TodoZoneReminders> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        SyncZoneOptions();
        // The helpers can be created a moment after startup, and zones come and go
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(15), SyncZoneOptions);

        // A text field is only saved when you leave it, which is the same tap that presses the button: give that a moment
        new Entity(HaContext, AddButton).StateChanges().Subscribe(_ => Scheduler.Schedule(ButtonDelay, AddItem));

        (string Name, string PersonId, string Whatsapp, string List)[] people =
        [
            ("Vincent", "person.vincent_maarschalkerweerd", Vincent.Whatsapp, "todo.vincent"),
            ("Carleen", "person.carleen", Carleen.Whatsapp, "todo.carleen")
        ];
        foreach (var (name, personId, whatsapp, list) in people)
            new Entity(HaContext, personId).StateChanges()
                .Where(x => x.Old?.State != x.New?.State)
                .Subscribe(x => _ = RemindAsync(name, whatsapp, list, x.New?.State));
    }

    /// <summary>The names of the zones as the person entity reports them while someone is in one (trimmed).</summary>
    private List<string> ZoneNames() =>
        Entities.Zone.EnumerateAll()
            .Select(z => z.Attributes?.FriendlyName?.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void SyncZoneOptions()
    {
        try
        {
            List<string> options = [NoZone, .. ZoneNames()];
            var current = new List<string>();
            if (HaContext.GetState(ZoneHelper)?.AttributesJson is { } attributes &&
                attributes.TryGetProperty("options", out var existing) && existing.ValueKind == JsonValueKind.Array)
                current = existing.EnumerateArray().Select(x => x.GetString() ?? "").ToList();

            if (current.SequenceEqual(options)) return;
            HaContext.CallService("input_select", "set_options", ServiceTarget.FromEntity(ZoneHelper), new { options });
            Logger.LogInformation("Zone choices for tasks: {Zones}", string.Join(", ", options));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not sync the zone choices for tasks");
        }
    }

    /// <summary>Puts the task of the form on its list, with the place in the description, and clears the form.</summary>
    private void AddItem()
    {
        try
        {
            var text = HaContext.GetState(TextHelper)?.State?.Trim();
            if (string.IsNullOrEmpty(text) || text is "unknown" or "unavailable")
            {
                Logger.LogInformation("Add task pressed without a text, ignored");
                return;
            }

            var listName = HaContext.GetState(ListHelper)?.State ?? "";
            if (!Lists.TryGetValue(listName, out var list))
            {
                Logger.LogWarning("Add task: unknown list '{List}'", listName);
                return;
            }

            var zone = HaContext.GetState(ZoneHelper)?.State;
            var message = HaContext.GetState(MessageHelper)?.State == "on";
            var data = new Dictionary<string, object> { ["item"] = text };
            if (!string.IsNullOrEmpty(zone) && zone != NoZone && zone is not ("unknown" or "unavailable"))
                data["description"] = $"{Pin} {zone}{(message ? $" {Bell}" : "")}";

            HaContext.CallService("todo", "add_item", ServiceTarget.FromEntity(list), data);
            Logger.LogInformation("Task '{Text}' added to {List}{Place}", text, list,
                data.TryGetValue("description", out var d) ? $" ({d})" : "");

            HaContext.CallService("input_text", "set_value", ServiceTarget.FromEntity(TextHelper), new { value = "" });
            HaContext.CallService("input_select", "select_option", ServiceTarget.FromEntity(ZoneHelper), new { option = NoZone });
            HaContext.CallService("input_boolean", "turn_off", ServiceTarget.FromEntity(MessageHelper));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not add the task");
        }
    }

    /// <summary>The zone a person just walked into, or null when they left a zone or are between zones.</summary>
    private string? EnteredZone(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        if (state == "home") return HomeZoneName;
        return ZoneNames().FirstOrDefault(n => string.Equals(n, state.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task RemindAsync(string person, string whatsapp, string ownList, string? newState)
    {
        try
        {
            var zone = EnteredZone(newState);
            if (zone == null) return;

            if (_lastReminder.TryGetValue((person, zone), out var last) && Scheduler.Now - last < Cooldown) return;

            var tasks = new List<string>();
            foreach (var list in new[] { ownList, SharedList })
                foreach (var (summary, description) in await OpenItemsAsync(list))
                    if (ParsePlace(description) is { Message: true } place && SameZone(place.Zone, zone))
                        tasks.Add(list == SharedList ? $"{summary} (samen)" : summary);
            if (tasks.Count == 0) return;

            if (string.IsNullOrEmpty(whatsapp))
            {
                Logger.LogWarning("{Person} has tasks at {Zone} but no WhatsApp id is configured", person, zone);
                return;
            }

            _lastReminder[(person, zone)] = Scheduler.Now;
            var text = $"{Pin} Je bent bij {zone}. Dit moet nog:\n{string.Join("\n", tasks.Select(t => $"• {t}"))}";
            Services.Whatsapp.SendMessage(clientId: "default", to: whatsapp, body: new { text });
            Logger.LogInformation("Reminded {Person} at {Zone} of {Count} task(s)", person, zone, tasks.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not remind {Person} of the tasks at {State}", person, newState);
        }
    }

    /// <summary>The tasks of a list that are not done yet, as summary and description.</summary>
    private async Task<List<(string Summary, string? Description)>> OpenItemsAsync(string list)
    {
        var response = await HaContext.CallServiceWithResponseAsync("todo", "get_items", ServiceTarget.FromEntity(list),
            new Dictionary<string, object> { ["status"] = "needs_action" });
        var items = new List<(string, string?)>();
        if (response is { } root && root.TryGetProperty(list, out var entity) && entity.TryGetProperty("items", out var array))
            foreach (var item in array.EnumerateArray())
                items.Add((item.GetProperty("summary").GetString() ?? "",
                    item.TryGetProperty("description", out var d) ? d.GetString() : null));
        return items;
    }

    private static bool SameZone(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The place in the description of a task (<c>📍 Zone</c>, with <c>🔔</c> after it when a message is wanted), or null.</summary>
    public static (string Zone, bool Message)? ParsePlace(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var match = PlaceRegex().Match(description.Split('\n')[0].Trim());
        return match.Success ? (match.Groups["zone"].Value.Trim(), match.Groups["bell"].Success) : null;
    }

    [GeneratedRegex(@"^📍\s*(?<zone>.+?)\s*(?<bell>🔔)?$")]
    private static partial Regex PlaceRegex();
}
