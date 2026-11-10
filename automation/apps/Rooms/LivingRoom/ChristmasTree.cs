using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Core;
using Automation.Helpers;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.LivingRoom;

/// <summary>
/// The Christmas tree (a Hue Festavia string): it is on all day, so it goes on when Vincent gets up (the sleeping switch goes off) and
/// off when he goes to sleep (only while the lamps are there: they are taken away after the holidays). The button "Kerst" starts the living room scene Kerst and plays the Christmas playlist on the kitchen speaker.
/// </summary>
[NetDaemonApp(Id = nameof(ChristmasTree))]
public class ChristmasTree : BaseApp
{
    public const string Tree = "light.festavia_string_lights_1";

    /// <summary>The speaker the Christmas music plays on: the kitchen.</summary>
    public const string Speaker = "media_player.nestmini9818";

    /// <summary>Pressing this button starts the Christmas playlist.</summary>
    public const string MusicButton = "input_button.start_kerstmuziek";

    private readonly ISpotcast _spotcast;
    private readonly AppConfig _config;

    public ChristmasTree(
        IHaContext ha,
        ILogger<ChristmasTree> logger,
        INotify notify,
        IScheduler scheduler,
        ISpotcast spotcast,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _spotcast = spotcast;
        _config = config.Value;

        Entities.InputBoolean.Sleepingvincent.WhenTurnsOff(_ => SetTree(on: true));
        Entities.InputBoolean.Sleepingvincent.WhenTurnsOn(_ => SetTree(on: false));

        // The tree stays on: anything else that switches it off (a scene, a group of lights, a "everything off") is undone, unless he sleeps
        Entities.Light.FestaviaStringLights1.StateChanges()
            .Where(x => x.Old?.State == "on" && x.New?.State == "off")
            .Subscribe(_ => Scheduler.Schedule(KeepOnDelay, KeepOn));

        // An input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press
        Entities.InputButton.StartKerstmuziek.StateChanges()
            .Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
            .Subscribe(_ => StartChristmas());
    }

    /// <summary>A short wait, so a group that switches its lights off one by one is done before the tree goes on again.</summary>
    private static readonly TimeSpan KeepOnDelay = TimeSpan.FromSeconds(2);

    private void KeepOn()
    {
        if (Vincent.IsSleeping || HaContext.GetState(Tree)?.State != "off") return;
        ActivityContext.Note("De kerstboom blijft aan, behalve als Vincent slaapt");
        SetTree(on: true);
    }

    private void SetTree(bool on)
    {
        // The lamps are taken away after the holidays: then there is nothing to switch
        if (HaContext.GetState(Tree)?.State is null or "unavailable" or "unknown")
        {
            Logger.LogDebug("The Christmas tree is not there, nothing to switch");
            return;
        }

        if (on) Entities.Light.FestaviaStringLights1.TurnOn();
        else Entities.Light.FestaviaStringLights1.TurnOff();
        Logger.LogInformation("Christmas tree {State}", on ? "on" : "off");
    }

    /// <summary>The tile Kerst on the dashboard: the living room scene "Kerst" (the Hue scene Sneeuwschittering) and the Christmas music.</summary>
    private void StartChristmas()
    {
        Entities.Scene.WoonkamerWoonkamerSneeuwschittering.TurnOn();
        Logger.LogInformation("Christmas scene started");
        PlayMusic();
    }

    private void PlayMusic()
    {
        var (url, account) = SpotifyChoice.Resolve(HaContext, _config, SpotifyRoom.Kerst, Vincent.IsHome, Carleen.IsHome);
        if (string.IsNullOrEmpty(url))
        {
            Logger.LogWarning("No Christmas playlist (not set on the Muziek page, not in config.json as SpotifyKerstUrl), no Christmas music");
            return;
        }
        _spotcast.PlaySpotify(Entities.MediaPlayer.Nestmini9818, url, account);
        Logger.LogInformation("Christmas music started");
    }
}
