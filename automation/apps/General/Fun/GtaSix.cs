using System.Reactive.Concurrency;
using System.Text.Json;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Fun;

/// <summary>
/// GTA VI comes out on 19 November 2026 at 00:00. With the switch "GTA 6 modus" on (the page of Vincent) the dashboard looks like Vice City and counts
/// down; at the moment it comes out Vincent gets a message on his phone.
/// </summary>
[NetDaemonApp(Id = nameof(GtaSix))]
public class GtaSix : BaseApp
{
    public const string Switch = "input_boolean.gta_6_modus";

    /// <summary>The button on the dashboard: every colour light in the house goes Vice City; the second press puts the lights back as they were.</summary>
    public const string LightButton = "input_button.gta_6_verlichting";

    /// <summary>What the lights were before (a scene made from their state).</summary>
    public const string ScenePrevious = "scene.gta6_voor";

    /// <summary>Pink, orange, cyan, purple and yellow: the colours of Vice City.</summary>
    public static readonly int[][] Palette = [[255, 46, 147], [255, 122, 26], [25, 227, 217], [180, 76, 255], [255, 210, 63]];

    private bool _lightsAreViceCity;

    /// <summary>The moment GTA VI comes out, in Dutch time.</summary>
    public static readonly DateTime Release = new(2026, 11, 19, 0, 0, 0);

    public GtaSix(
        IHaContext ha,
        ILogger<GtaSix> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        // Cron has no year: it runs every 19 November, and only the day of the release counts
        Scheduler.ScheduleCronNl("0 0 19 11 *", ReleaseMoment);

        // An input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press
        new Entity(HaContext, LightButton).StateChanges()
            .Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
            .Subscribe(_ => ToggleLights());
    }

    /// <summary>Whether a light can show a colour (not only white or a colour temperature).</summary>
    public static bool IsColorLight(JsonElement? attributes) =>
        attributes is { ValueKind: JsonValueKind.Object } a
        && !a.TryGetProperty("entity_id", out _) // a group: its lights are done one by one
        && a.TryGetProperty("supported_color_modes", out var modes) && modes.ValueKind == JsonValueKind.Array
        && modes.EnumerateArray().Any(m => m.GetString() is "hs" or "xy" or "rgb" or "rgbw" or "rgbww");

    private void ToggleLights()
    {
        if (_lightsAreViceCity)
        {
            _lightsAreViceCity = false;
            Logger.LogInformation("Vice City lights off: the lights go back as they were");
            HaContext.CallService("scene", "turn_on", ServiceTarget.FromEntity(ScenePrevious));
            return;
        }

        // Nothing changes while somebody sleeps or nobody is home
        if (IsSleepMode || Entities.InputBoolean.Away.IsOn()) return;

        var lights = HaContext.GetAllEntities()
            .Where(e => e.EntityId.StartsWith("light.", StringComparison.Ordinal) && e.EntityState?.State is "on" or "off" && IsColorLight(e.EntityState?.AttributesJson))
            .Select(e => e.EntityId).Order().ToList();
        if (lights.Count == 0) return;

        Logger.LogInformation("Vice City lights: {Count} lights", lights.Count);
        HaContext.CallService("scene", "create", data: new { scene_id = "gta6_voor", snapshot_entities = lights });
        _lightsAreViceCity = true;
        for (var i = 0; i < lights.Count; i++)
            HaContext.CallService("light", "turn_on", ServiceTarget.FromEntity(lights[i]), new { rgb_color = Palette[i % Palette.Length], brightness_pct = 75 });
    }

    private void ReleaseMoment()
    {
        var now = Scheduler.NowNl().DateTime;
        if (now.Year != Release.Year || HaContext.GetState(Switch)?.State != "on") return;

        Logger.LogInformation("GTA VI is out");
        Notify.NotifyPhoneVincent("🌴 GTA VI is uit!", "Het is zover: 19 november, 00:00. Veel plezier in Vice City!", true);
    }
}
