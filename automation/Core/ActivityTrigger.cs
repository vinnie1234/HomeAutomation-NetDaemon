using System.Globalization;
using System.Text.Json;

namespace Automation.Core;

/// <summary>
/// What made an app do something: a state that changed (a motion sensor that went on), an event (a button, a WhatsApp message) or the time
/// (a schedule or a timer that ran out). A timer that was started by something else keeps that: "5 min later, after: motion in the living
/// room". The activity log writes it with every action so the page Activiteit can say why it happened.
/// </summary>
public sealed class ActivityTrigger
{
    private readonly Lazy<string> _text;

    /// <param name="kind">"entiteit", "event" or "tijd".</param>
    /// <param name="text">The sentence, made only when the action is written down (most triggers lead to nothing).</param>
    /// <param name="entity">The entity that changed, if it was one.</param>
    /// <param name="parent">What started the timer, for a time trigger that was started by something else.</param>
    public ActivityTrigger(string kind, Func<string> text, string? entity = null, ActivityTrigger? parent = null)
    {
        Kind = kind;
        _text = new Lazy<string>(text);
        Entity = entity;
        Parent = parent;
    }

    public string Kind { get; }
    public string Text => _text.Value;
    public string? Entity { get; }
    public ActivityTrigger? Parent { get; }

    /// <summary>The cause first and then what followed from it: ["Beweging Woonkamer: uit → aan", "5 min later"].</summary>
    public IReadOnlyList<string> Chain => [.. Parent?.Chain ?? [], Text];

    /// <summary>The entity of the first cause in the chain (the one that really started it), if there is one.</summary>
    public string? RootEntity => Parent?.RootEntity ?? Entity;

    /// <summary>The kind of the first cause in the chain.</summary>
    public string RootKind => Parent?.RootKind ?? Kind;

    // ---- the triggers

    /// <summary>A state that changed.</summary>
    public static ActivityTrigger FromState(StateChange change) =>
        new("entiteit", () => Describe(change), IdOf(change));

    private static string? IdOf(StateChange change) => change.Entity?.EntityId ?? change.New?.EntityId ?? change.Old?.EntityId;

    /// <summary>An event on the bus (not the high-volume ones, see <see cref="ActivityContext.IgnoredEvents"/>).</summary>
    public static ActivityTrigger FromEvent(Event e) => new("event", () => Describe(e));

    /// <summary>A schedule or timer that ran: at a time, or after a wait that something else started.</summary>
    public static ActivityTrigger FromTime(ActivityTrigger? startedBy, TimeSpan? wait, DateTimeOffset? at) =>
        new("tijd", () => startedBy == null
            ? (at is { } due ? $"Tijd: gepland om {Extensions.SchedulerExtensions.ToDutchTime(due):HH:mm:ss}" : $"Timer van {Wait(wait ?? TimeSpan.Zero)} afgelopen")
            : $"{Wait(wait ?? TimeSpan.Zero)} later", parent: startedBy);

    // ---- the words

    private static string Describe(StateChange change)
    {
        var name = FriendlyName(change.New) ?? FriendlyName(change.Old) ?? IdOf(change) ?? "onbekend";
        var motion = Attribute(change.New, "device_class") is "motion" or "occupancy" or "presence";
        if (motion && change.New?.State == "on") return $"Beweging: {name}";
        if (motion && change.New?.State == "off") return $"Geen beweging meer: {name}";
        return $"{name}: {State(change.Old?.State)} → {State(change.New?.State)}";
    }

    private static string Describe(Event e) => e.EventType switch
    {
        "hue_event" => "Hue-knop ingedrukt" + (e.DataElement is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("id", out var id) ? $" ({id.GetString()})" : ""),
        "new_whatsapp_message" => "WhatsApp-bericht ontvangen",
        "mobile_app_notification_action" => "Knop in een melding ingedrukt",
        _ => $"Event {e.EventType}"
    };

    private static string State(string? state) => state switch
    {
        null or "" => "onbekend",
        "on" => "aan",
        "off" => "uit",
        "home" => "thuis",
        "not_home" => "weg",
        "unavailable" => "niet beschikbaar",
        "unknown" => "onbekend",
        "playing" => "speelt",
        "idle" => "inactief",
        "open" => "open",
        "closed" => "dicht",
        _ => state.Length > 40 ? state[..39] + "…" : state
    };

    private static string Wait(TimeSpan span) => span.TotalSeconds switch
    {
        < 90 => $"{Math.Max(1, (int)Math.Round(span.TotalSeconds))} s",
        < 5400 => $"{(int)Math.Round(span.TotalMinutes)} min",
        _ => $"{span.TotalHours.ToString("0.#", CultureInfo.GetCultureInfo("nl-NL"))} uur"
    };

    private static string? Attribute(EntityState? state, string name) =>
        state?.AttributesJson is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? FriendlyName(EntityState? state) =>
        Attribute(state, "friendly_name") is { Length: > 0 } name ? name : null;
}

/// <summary>
/// The trigger that is running at this moment, per flow of execution: it follows the work that was started because of it, also after
/// a wait (a timer that was started inside it takes it along). That is how an action can say what caused it without the apps doing anything.
/// </summary>
public static class ActivityContext
{
    /// <summary>Events that come by all the time and are not a reason for anything an app does; they do not start a trigger.</summary>
    public static readonly string[] IgnoredEvents = ["state_changed", "call_service", ActivityLog.EventType, "service_registered", "entity_registry_updated", "device_registry_updated"];

    private static readonly AsyncLocal<ActivityTrigger?> Local = new();

    public static ActivityTrigger? Current => Local.Value;

    /// <summary>Makes <paramref name="trigger"/> the current one until the scope is disposed.</summary>
    public static IDisposable Begin(ActivityTrigger trigger)
    {
        var previous = Local.Value;
        Local.Value = trigger;
        return new Scope(previous);
    }

    private sealed class Scope(ActivityTrigger? previous) : IDisposable
    {
        public void Dispose() => Local.Value = previous;
    }
}
