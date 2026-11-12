using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Enum;
using Automation.Helpers;
using Microsoft.Extensions.Options;
using static Automation.Globals;

namespace Automation.apps.Rooms.LivingRoom;

/// <summary>
/// Date night (the switch "Date night"): the living room lights turn romantic, a question on the phones whether the music should change to the date night playlist (living room and kitchen), the
/// vacuum does not run and the other automatic music, the announcements and the ordinary notifications stay quiet (see
/// <see cref="Helpers.SpotifyChoice.Autoplay"/>, the vacuum and <see cref="Notify"/>). The dashboard turns calm and romantic by itself.
/// Date night always ends when someone goes to sleep. When it ends, the music stops and the living room lights go back to the scene of the time of day.
/// </summary>
[NetDaemonApp(Id = nameof(DateNight))]
public class DateNight : BaseApp
{
    /// <summary>How long the question about the music stays on the phones, in seconds.</summary>
    public const int MusicQuestionTimeout = 30;

    public const string PlaylistUrl = "https://open.spotify.com/playlist/1HLh1SsPFrBOQLta0y2FFl";

    private readonly ISpotcast _spotcast;
    private readonly AppConfig _config;

    public DateNight(
        IHaContext ha,
        ILogger<DateNight> logger,
        INotify notify,
        IScheduler scheduler,
        ISpotcast spotcast,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _spotcast = spotcast;
        _config = config.Value;

        Entities.InputBoolean.Datenight.WhenTurnsOn(_ => Start());
        Entities.InputBoolean.Datenight.WhenTurnsOff(_ => End());

        // Going to sleep always ends date night
        Entities.InputBoolean.Sleepingvincent.WhenTurnsOn(_ => EndBecauseOfSleep());
        Entities.InputBoolean.Sleepingcarleen.WhenTurnsOn(_ => EndBecauseOfSleep());
    }

    private void EndBecauseOfSleep()
    {
        if (Entities.InputBoolean.Datenight.IsOff()) return;
        Logger.LogInformation("Someone goes to sleep: date night ends");
        Entities.InputBoolean.Datenight.TurnOff();
    }

    private void Start()
    {
        Logger.LogInformation("Date night starts");
        RomanticLights();

        // The vacuum has to be docked; it does not start by itself during date night (see Vacuum)
        if (Entities.Vacuum.Jaap.State == "cleaning")
        {
            Logger.LogInformation("The vacuum goes back to its dock for date night");
            Entities.Vacuum.Jaap.ReturnToBase();
        }

        // The music is only changed when someone says so: a question on both phones that goes away by itself
        Notify.NotifyPhoneVincentCarleen("Date night", "Muziek aanpassen naar de date night playlist?", true,
            action:
            [
                new ActionModel("DATENIGHT_MUSIC_YES", "Ja", func: PlayMusic),
                new ActionModel("DATENIGHT_MUSIC_NO", "Nee", func: () => Logger.LogInformation("Date night: the music stays as it is"))
            ],
            timeoutSeconds: MusicQuestionTimeout);
    }

    /// <summary>The date night playlist, on the speakers of the living room and the kitchen.</summary>
    private void PlayMusic()
    {
        var (_, account) = SpotifyChoice.Resolve(HaContext, _config, SpotifyRoom.Keuken, Vincent.IsHome, Carleen.IsHome);
        _spotcast.PlaySpotify(Entities.MediaPlayer.Woonkamer, PlaylistUrl, account);
        _spotcast.PlaySpotify(Entities.MediaPlayer.Nestmini9818, PlaylistUrl, account);
        Logger.LogInformation("Date night playlist started in the living room and the kitchen");
    }

    /// <summary>Warm: the pendant lamp at 80 percent (games are played at date night too), the bench and the Hue Play lights in a deep rose, the rest of the room out.</summary>
    private void RomanticLights()
    {
        Entities.Light.PlafondWoonkamer.TurnOn(brightnessPct: 80, colorTempKelvin: 2200);
        Entities.Light.HueFilamentBulb1.TurnOn(brightnessPct: 80, colorTempKelvin: 2200);
        Entities.Light.HueFilamentBulb2.TurnOn(brightnessPct: 80, colorTempKelvin: 2200);
        foreach (var play in new[] { Entities.Light.HuePlayLinks, Entities.Light.HuePlayMidden, Entities.Light.HuePlayRechts })
            play.TurnOn(brightnessPct: 30, rgbColor: [255, 40, 90]);
        Entities.Light.Bank.TurnOn(brightnessPct: 25, rgbColor: [255, 40, 90]);
        Entities.Light.LampenKeuken.TurnOff();
    }

    private void End()
    {
        Logger.LogInformation("Date night ends");
        if (Entities.MediaPlayer.Woonkamer.State == "playing") Entities.MediaPlayer.Woonkamer.MediaPause();
        if (Entities.MediaPlayer.Nestmini9818.State == "playing") Entities.MediaPlayer.Nestmini9818.MediaPause();

        // Asleep or away: the lights are the business of the sleep and leaving routines
        if (IsSleepMode || Entities.InputBoolean.Away.IsOn() || Entities.InputBoolean.Disablelightautomationlivingroom.IsOn()) return;

        switch (GetHouseState(Entities))
        {
            case HouseState.Morning: Entities.Scene.Woonkamermorning.TurnOn(); break;
            case HouseState.Evening: Entities.Scene.Woonkamerevening.TurnOn(); break;
            case HouseState.Night: Entities.Scene.Woonkamernight.TurnOn(); break;
            default: Entities.Scene.Woonkamerday.TurnOn(); break;
        }
    }
}
