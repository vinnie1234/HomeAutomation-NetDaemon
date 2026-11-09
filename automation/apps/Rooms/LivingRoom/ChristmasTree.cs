using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Interfaces;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.LivingRoom;

/// <summary>
/// The Christmas tree (a Hue Festavia string): it is on all day, so it goes on when Vincent gets up (the sleeping switch goes off) and
/// off when he goes to sleep. The button "Kerstmuziek" plays the Christmas playlist on the kitchen speaker.
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

        // An input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press
        new Entity(HaContext, MusicButton).StateChanges()
            .Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
            .Subscribe(_ => PlayMusic());
    }

    private void SetTree(bool on)
    {
        HaContext.CallService("light", on ? "turn_on" : "turn_off", ServiceTarget.FromEntity(Tree));
        Logger.LogInformation("Christmas tree {State}", on ? "on" : "off");
    }

    private void PlayMusic()
    {
        if (string.IsNullOrEmpty(_config.SpotifyKerstUrl))
        {
            Logger.LogWarning("No SpotifyKerstUrl in config.json, no Christmas music");
            return;
        }
        _spotcast.PlaySpotify(new MediaPlayerEntity(HaContext, Speaker), _config.SpotifyKerstUrl);
        Logger.LogInformation("Christmas music started");
    }
}
