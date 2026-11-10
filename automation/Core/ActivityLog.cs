using System.Globalization;
using System.Text.RegularExpressions;
using Automation.apps.General.System;

namespace Automation.Core;

/// <summary>
/// The activity log of NetDaemon: every action an app takes in Home Assistant (a light that goes on, a scene, a message, a status that
/// changes) becomes one event <c>netdaemon_activity</c> with who did it (the app), what, where (the room) and for whom (the person).
/// <see cref="ActivityRecorder"/> turns each event into a state of the sensor <c>sensor.netdaemon_activiteit</c>, which
/// NetDaemon creates itself; Home Assistant keeps it in its history, and the dashboard page Activiteit reads that history and filters it.
/// This replaces reading the text logs.
/// </summary>
public static class ActivityLog
{
    /// <summary>The event type Home Assistant gets.</summary>
    public const string EventType = "netdaemon_activity";

    /// <summary>Things NetDaemon does all the time that nobody wants to read: the heartbeat, and the log itself.</summary>
    private static readonly string[] Ignored =
    [
        "input_datetime.netdaemon_heartbeat", "input_boolean.netdaemon_net_daemon_heartbeat", "sensor.netdaemon_mqtt_heartbeat", "sensor.netdaemon_activiteit"
    ];

    private static readonly string[] IgnoredDomains = ["logbook"];

    private static long _counter;

    /// <summary>The last status written down per entity: NetDaemon sets some sensors again and again with the same value.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> LastStatus = new();

    /// <summary>When the status of an entity was last written down.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> LastStatusAt = new();

    /// <summary>How long a status that is a number is left alone after it was written down.</summary>
    private static readonly TimeSpan NumberInterval = TimeSpan.FromMinutes(30);

    /// <summary>The conditions of an action, one line each: what the app wrote down, then the states it looked at.</summary>
    private static string[] ConditionsOf(IHaContext inner, ActivityTrigger? cause) =>
        cause == null
            ? []
            : [.. cause.Notes, .. cause.Conditions.Where(c => !Ignored.Contains(c.Entity))
                .Select(c => $"{NameOf(inner, c.Entity)}: {ActivityTrigger.StateText(c.State)}")];

    /// <summary>
    /// The actions from the first moments after NetDaemon started, before <see cref="ActivityRecorder"/> was
    /// listening: the apps wake up in no fixed order and many of them act at once. The recorder takes them over when it starts.
    /// </summary>
    public static ActivityBacklog Backlog { get; } = new();

    private static readonly (string Room, string[] Words)[] Rooms =
    [
        ("Woonkamer", ["woonkamer", "tv", "bank", "soundbar", "hanglamp", "hue_play", "hueplay", "kerstboom", "festavia", "bot29ff"]),
        ("Keuken", ["keuken", "nestmini9818", "lampen_keuken", "koelkast"]),
        ("Badkamer", ["badkamer", "douche", "googlehome0351", "wasmachine", "spiegel"]),
        ("Slaapkamer", ["slaapkamer", "nachtkastje", "bed"]),
        ("Gang", ["gang", "hal", "googlehome7421", "googlehome8627"]),
        ("Berging", ["berging"]),
        ("Toilet", ["toilet", "wc"]),
        ("Buiten", ["buiten", "voordeur", "tuin", "oprit", "brievenbus"])
    ];

    private static readonly string[] People = ["vincent", "carleen"];

    /// <summary>The room an app belongs to, from the folder it lives in (<c>apps/Rooms/Kitchen</c> is the kitchen); null for the other apps.</summary>
    public static string? RoomOfApp(Type app) => app.Namespace?.Split('.').LastOrDefault() switch
    {
        "LivingRoom" => "Woonkamer",
        "Kitchen" => "Keuken",
        "BathRoom" => "Badkamer",
        "BedRoom" => "Slaapkamer",
        "Hall" => "Gang",
        _ => null
    };

    /// <summary>Records an action that an app took on Home Assistant (a service call).</summary>
    /// <param name="inner">The context to fire the event on (never the one that records, or it would record itself).</param>
    /// <param name="app">The app that did it.</param>
    /// <param name="domain">The domain of the service.</param>
    /// <param name="service">The service.</param>
    /// <param name="target">What the call is aimed at, when it has a target.</param>
    /// <param name="data">The data of the call.</param>
    /// <param name="knownPeople">Messaging ids of persons (WhatsApp ids), so a message to someone counts for that person.</param>
    /// <param name="appRoom">The room of the app, for an action whose entities say nothing about a room.</param>
    public static void Service(IHaContext inner, string app, string domain, string service, ServiceTarget? target, object? data,
        IReadOnlyDictionary<string, string>? knownPeople = null, string? appRoom = null)
    {
        try
        {
            var entities = target?.EntityIds?.ToList() ?? [];
            if (IgnoredDomains.Contains(domain) || entities.Count > 0 && entities.All(e => Ignored.Contains(e))) return;

            var json = ToJson(data);
            // The entities can also be in the data of the call (input_boolean.turn_on with an entity_id, scene.apply with entities, ...)
            if (entities.Count == 0 && json is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("entity_id", out var id))
                entities.AddRange(id.ValueKind == JsonValueKind.Array ? id.EnumerateArray().Select(x => x.GetString() ?? "") : [id.GetString() ?? ""]);
            entities.RemoveAll(string.IsNullOrEmpty);

            var names = entities.Select(e => NameOf(inner, e)).ToList();
            var text = Describe(domain, service, names, json);
            var haystack = string.Join(' ', entities.Concat(names).Append(service).Append(domain).Append(text));
            var people = People.Where(p => haystack.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();
            if (knownPeople != null && json is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty("to", out var to) &&
                to.ValueKind == JsonValueKind.String && knownPeople.TryGetValue(to.GetString() ?? "", out var named) && !people.Contains(named))
                people.Add(named);

            var cause = ActivityContext.Current;
            Fire(inner, new
            {
                id = NextId(),
                time = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                app,
                kind = "actie",
                service = $"{domain}.{service}",
                entity = entities.FirstOrDefault() ?? "",
                cause = CauseText(cause),
                causeKind = cause?.RootKind ?? "geen",
                causeEntity = cause?.RootEntity ?? "",
                causeChain = cause?.Chain ?? [],
                conditions = ConditionsOf(inner, cause),
                data = DataText(json),
                text,
                room = string.Join(", ", RoomsFor(inner, entities, entities.Concat(names).Append(text)) is { Count: > 0 } rooms ? rooms : appRoom is null ? [] : [appRoom]),
                person = string.Join(", ", people.Select(Capitalize))
            });
        }
        catch
        {
            // Writing the log must never get in the way of what the app does
        }
    }

    /// <summary>Records that NetDaemon changed the status of an entity it manages itself (a sensor it created).</summary>
    public static void Status(IHaContext inner, string app, string entityId, string state)
    {
        try
        {
            if (Ignored.Contains(entityId)) return;
            // Only a status that changed is news: the same value again is not written down
            if (LastStatus.TryGetValue(entityId, out var last) && last == state) return;
            // A number that counts along (the cost of working at home, the working time) changes every minute: once in a while is enough,
            // Home Assistant has the history of the sensor itself. A status in words, or a number that was not written down yet, always counts.
            var now = DateTimeOffset.UtcNow;
            var isNumber = double.TryParse(state, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            if (isNumber && LastStatusAt.TryGetValue(entityId, out var writtenAt) && now - writtenAt < NumberInterval) return;
            LastStatus[entityId] = state;
            LastStatusAt[entityId] = now;
            var text = $"{NameOf(inner, entityId)} wordt {Trim(state, 80)}";
            var people = People.Where(p => entityId.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();
            var cause = ActivityContext.Current;
            Fire(inner, new
            {
                id = NextId(),
                time = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                app,
                kind = "status",
                service = "",
                entity = entityId,
                cause = CauseText(cause),
                causeKind = cause?.RootKind ?? "geen",
                causeEntity = cause?.RootEntity ?? "",
                causeChain = cause?.Chain ?? [],
                conditions = ConditionsOf(inner, cause),
                data = "",
                text,
                room = string.Join(", ", RoomsFor(inner, [entityId], [entityId])),
                person = string.Join(", ", people.Select(Capitalize))
            });
        }
        catch
        {
            // see Service
        }
    }

    /// <summary>The rooms an action touches: the areas the entities have in Home Assistant, and where they have none the words in them.</summary>
    private static List<string> RoomsFor(IHaContext ha, IEnumerable<string> entities, IEnumerable<string> texts)
    {
        var rooms = new List<string>();
        foreach (var entity in entities)
        {
            var area = ha.GetAreaFromEntityId(entity)?.Name;
            if (!string.IsNullOrWhiteSpace(area) && !rooms.Contains(area, StringComparer.OrdinalIgnoreCase)) rooms.Add(area);
        }
        return rooms.Count > 0 ? rooms : [.. RoomsOf(texts)];
    }

    /// <summary>The rooms an action touches: from the words in the entities, their names and the text.</summary>
    public static IEnumerable<string> RoomsOf(IEnumerable<string> texts)
    {
        var all = string.Join(' ', texts).ToLowerInvariant();
        foreach (var (room, words) in Rooms)
            if (words.Any(w => w.Length >= 5 ? all.Contains(w, StringComparison.Ordinal) : Regex.IsMatch(all, $"(?<![a-z0-9]){w}(?![a-z0-9])")))
                yield return room;
    }

    /// <summary>Why it happened, in one line: the cause and what followed from it. Nothing found: say so.</summary>
    private static string CauseText(ActivityTrigger? cause) =>
        cause == null ? "Geen aanleiding gevonden: de app deed dit uit zichzelf (bijvoorbeeld bij het opstarten)" : string.Join(" › ", cause.Chain);

    /// <summary>What was sent, compact and shortened, so the page can show it.</summary>
    private static string DataText(JsonElement? data) => data is { ValueKind: not JsonValueKind.Null } d ? Trim(d.GetRawText(), 400) : "";

    private static string NextId() => $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Interlocked.Increment(ref _counter)}";

    private static void Fire(IHaContext inner, object data)
    {
        Backlog.Add(JsonSerializer.SerializeToElement(data));
        inner.SendEvent(EventType, data);
    }

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string NameOf(IHaContext ha, string entityId)
    {
        var name = ha.GetState(entityId)?.AttributesJson is { ValueKind: JsonValueKind.Object } a &&
                   a.TryGetProperty("friendly_name", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
        return string.IsNullOrWhiteSpace(name) ? entityId : name;
    }

    private static JsonElement? ToJson(object? data)
    {
        if (data == null) return null;
        try { return JsonSerializer.SerializeToElement(data); }
        catch { return null; }
    }

    /// <summary>What happened, in a sentence: the entities and then the action.</summary>
    private static string Describe(string domain, string service, IReadOnlyList<string> names, JsonElement? data)
    {
        var what = names.Count switch
        {
            0 => domain,
            <= 3 => string.Join(", ", names),
            _ => $"{names[0]}, {names[1]} en {names.Count - 2} andere"
        };
        var action = $"{domain}.{service}" switch
        {
            "light.turn_on" or "switch.turn_on" or "input_boolean.turn_on" or "media_player.turn_on" or "fan.turn_on" => "aan",
            "light.turn_off" or "switch.turn_off" or "input_boolean.turn_off" or "media_player.turn_off" or "fan.turn_off" => "uit",
            "light.toggle" or "switch.toggle" or "input_boolean.toggle" => "omgeschakeld",
            "scene.turn_on" => "scene geactiveerd",
            "button.press" or "input_button.press" => "ingedrukt",
            "whatsapp.send_message" => "bericht verstuurd",
            "spotcast.play_media" => "muziek gestart",
            "media_player.volume_set" => "volume gezet",
            "media_player.media_play" => "speelt af",
            "media_player.media_pause" or "media_player.media_stop" => "gestopt",
            "input_select.select_option" => "keuze gezet",
            "input_text.set_value" or "input_number.set_value" or "input_datetime.set_datetime" => "waarde gezet",
            "todo.add_item" => "taak toegevoegd",
            "todo.update_item" => "taak bijgewerkt",
            "todo.remove_completed_items" => "afgevinkte taken verwijderd",
            _ when domain == "notify" => "melding verstuurd",
            _ => $"{service.Replace('_', ' ')}"
        };
        var detail = Detail(data);
        // A message: what it said, so the log shows more than that one was sent
        var said = Said(domain, data);
        return $"{what}: {action}{(detail.Length > 0 ? $" ({detail})" : "")}{(said.Length > 0 ? $" \u201c{said}\u201d" : "")}";
    }

    /// <summary>The words of a message (a notification, a WhatsApp message), shortened.</summary>
    private static string Said(string domain, JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } d) return "";
        string? Get(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var text = domain switch
        {
            "whatsapp" when d.TryGetProperty("body", out var body) => Get(body, "text"),
            "notify" => Get(d, "message") is { Length: > 0 } m
                ? Get(d, "title") is { Length: > 0 } t ? $"{t}: {m}" : m
                : Get(d, "title"),
            _ => null
        };
        return string.IsNullOrWhiteSpace(text) ? "" : Trim(text.Replace('\n', ' ').Trim(), 90);
    }

    /// <summary>The numbers and options of a call worth reading: brightness, colour temperature, volume, the option chosen.</summary>
    private static string Detail(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } d) return "";
        var parts = new List<string>();
        void Add(string key, string label, string suffix = "")
        {
            if (d.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.Number or JsonValueKind.String)
                parts.Add($"{label} {Trim(v.ToString(), 60)}{suffix}");
        }
        Add("brightness_pct", "helderheid", "%");
        Add("brightness", "helderheid");
        Add("color_temp_kelvin", "kleur", " K");
        Add("effect", "effect");
        Add("transition", "overgang", " s");
        Add("volume_level", "volume");
        Add("option", "keuze");
        Add("value", "waarde");
        Add("item", "item");
        return string.Join(", ", parts);
    }
}
