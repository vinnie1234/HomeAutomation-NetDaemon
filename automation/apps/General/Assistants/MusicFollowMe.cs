using System.Reactive.Concurrency;
using System.Text.Json;
using NetDaemon.HassModel.Entities;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Assistants;

/// <summary>
/// Music that goes along with Vincent.
/// <list type="bullet">
/// <item><b>The laptop</b> (switch "Muziek volgt laptop"): when the work laptop locks while the music plays on it, the music goes on in the
/// kitchen; when the laptop unlocks it goes back to the laptop.</item>
/// <item><b>Motion</b> (switch "Muziek volgt mij"): when there is motion in a room with a speaker (living room, bathroom, hall) and music plays
/// somewhere else, the music moves to that room. Nothing moves while the TV is on or date night is on.</item>
/// </list>
/// Nothing happens while someone sleeps or nobody is home. When both Vincent and Carleen are home, "Muziek volgt mij" goes off by itself (the music
/// would jump around between two people); when one of them leaves it goes on again. With Music Assistant on (Beheer) the music is moved with
/// Music Assistant (the queue moves along), otherwise with Spotify Connect. The laptop always goes through Spotify Connect: the Spotify app
/// on the laptop is not a Music Assistant player.
/// </summary>
[NetDaemonApp(Id = nameof(MusicFollowMe))]
public class MusicFollowMe : BaseApp
{
    public const string FollowSwitch = "input_boolean.muziek_volgt_mij";
    public const string LaptopSwitch = "input_boolean.muziek_volgt_laptop";

    /// <summary>The name of the laptop as a Spotify device.</summary>
    public const string LaptopDevice = "ADMIN_RISK182";

    /// <summary>The Spotify devices that count as the desk: the laptop, the Music Assistant Playback device and the TV.</summary>
    public static readonly string[] DeskSources = [LaptopDevice, "Music Assistant Playback", "Tv in Woonkamer", "Soundbar"];

    /// <summary>The kitchen as a Spotify device.</summary>
    public const string KitchenDevice = "Keuken";

    /// <summary>A room with a speaker: its motion sensor, the Cast speaker (and its name as a Spotify device) and the Music Assistant player.</summary>
    public sealed record Room(string Name, string Motion, string Cast, string SpotifyDevice, string MusicAssistant);

    public static readonly Room[] Rooms =
    [
        // Motion in the living room: the music plays in the kitchen (the speaker that is meant for it)
        new("Woonkamer", "binary_sensor.motionwoonkamer", "media_player.nestmini9818", "Keuken", "media_player.keuken_keuken"),
        new("Badkamer", "binary_sensor.badkamer_beweging", "media_player.googlehome0351", "Badkamer Speaker", "media_player.badkamer_badkamer_speaker"),
        new("Gang", "binary_sensor.gang_beweging", "media_player.friends_speakers", "Friends Speakers", "media_player.gang_friends_speakers"),
    ];

    /// <summary>The kitchen speaker, used by the laptop and found as a source of the music.</summary>
    public static readonly Room Kitchen = new("Keuken", "", "media_player.nestmini9818", KitchenDevice, "media_player.keuken_keuken");

    /// <summary>Every speaker the music can come from.</summary>
    private static readonly Room[] Sources =
    [
        new("Woonkamer", "binary_sensor.motionwoonkamer", "media_player.woonkamer", "Woonkamer", "media_player.woonkamer_woonkamer"),
        Kitchen,
        .. Rooms.Where(r => r.MusicAssistant != Kitchen.MusicAssistant)
    ];

    /// <summary>The Spotify player that moves music between devices (Spotify Connect).</summary>
    public const string SpotifyPlayer = "media_player.spotifyplus_vincent_maarschalkerweerd";

    /// <summary>The Spotify player that shows what plays on which device.</summary>
    public const string SpotifyState = "media_player.spotify_vincent_maarschalkerweerd";

    /// <summary>The music does not move again within this time, so walking through two rooms does not make it jump around.</summary>
    public static readonly TimeSpan MoveCooldown = TimeSpan.FromSeconds(45);

    private DateTimeOffset _lastMove = DateTimeOffset.MinValue;
    private bool _onKitchenBecauseOfLaptop;

    public MusicFollowMe(
        IHaContext ha,
        ILogger<MusicFollowMe> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        foreach (var room in Rooms)
        {
            var target = room;
            new Entity(HaContext, room.Motion).StateChanges()
                .Where(x => x.New?.State == "on")
                .Subscribe(_ => MotionIn(target));
        }

        new Entity(HaContext, "binary_sensor.werk_laptop_session_locked").StateChanges()
            .Where(x => x.Old?.State is "on" or "off" && x.New?.State is "on" or "off" && x.Old.State != x.New.State)
            .Subscribe(x => { if (x.New?.State == "on") LaptopLocked(); else LaptopUnlocked(); });

        // Two people home: the music does not follow anybody. One person home: it does.
        Entities.InputBoolean.Awayvincent.StateChanges().Merge(Entities.InputBoolean.Awaycarleen.StateChanges())
            .Subscribe(_ => SetFollowForPeopleHome());
    }

    // ---- the house

    private bool On(string entityId) => HaContext.GetState(entityId)?.State == "on";

    /// <summary>Music may only move around when somebody is home and awake.</summary>
    private bool HouseIsQuiet => !IsSleepMode && Entities.InputBoolean.Away.IsOff() && Vincent.IsHome;

    private bool UsesMusicAssistant => Spotcast.IsMusicAssistantOn(HaContext);

    private void SetFollowForPeopleHome()
    {
        var both = Vincent.IsHome && Carleen.IsHome;
        var one = Vincent.IsHome ^ Carleen.IsHome;
        if (both && On(FollowSwitch))
        {
            Logger.LogInformation("Both are home: the music no longer follows");
            SetSwitch(FollowSwitch, false);
        }
        else if (one && !On(FollowSwitch))
        {
            Logger.LogInformation("One person is home: the music follows again");
            SetSwitch(FollowSwitch, true);
        }
    }

    private void SetSwitch(string entityId, bool on) =>
        HaContext.CallService("input_boolean", on ? "turn_on" : "turn_off", ServiceTarget.FromEntity(entityId));

    // ---- motion

    private void MotionIn(Room room)
    {
        if (!On(FollowSwitch) || !HouseIsQuiet || On("input_boolean.datenight")) return;
        if (Entities.MediaPlayer.Tv.State is "on" or "playing" or "paused") return;
        if (Scheduler.Now - _lastMove < MoveCooldown) return;

        var source = PlayingRoom();
        if (source == null || source.Cast == room.Cast) return;

        Core.ActivityContext.Note($"Beweging in de {room.Name.ToLowerInvariant()}: de muziek uit de {source.Name.ToLowerInvariant()} gaat mee");
        Logger.LogInformation("Motion in {Room}: moving the music from {From}", room.Name, source.Name);
        _lastMove = Scheduler.Now;
        MoveMusic(source, room);
    }

    /// <summary>The room whose speaker plays music now, if there is one.</summary>
    private Room? PlayingRoom()
    {
        foreach (var room in Sources)
        {
            var playing = UsesMusicAssistant && HaContext.GetState(room.MusicAssistant)?.State == "playing"
                          || HaContext.GetState(room.Cast)?.State == "playing";
            if (playing) return room;
        }
        return null;
    }

    private void MoveMusic(Room from, Room to)
    {
        if (UsesMusicAssistant && HaContext.GetState(from.MusicAssistant)?.State == "playing")
        {
            HaContext.CallService("music_assistant", "transfer_queue", ServiceTarget.FromEntity(to.MusicAssistant),
                new { source_player = from.MusicAssistant, auto_play = true });
            return;
        }

        SpotifyConnectTo(to.SpotifyDevice);
    }

    private void SpotifyConnectTo(string device) =>
        HaContext.CallService("media_player", "select_source", ServiceTarget.FromEntity(SpotifyPlayer), new { source = device });

    // ---- the laptop

    private bool LaptopMusicMayMove => On(LaptopSwitch) && HouseIsQuiet && Entities.InputBoolean.Datenight.IsOff();

    private void LaptopLocked()
    {
        if (!LaptopMusicMayMove) return;
        var spotify = HaContext.GetState(SpotifyState);
        var source = Attribute(spotify, "source");
        // Music that belongs to the desk: on the laptop itself, or on the Music Assistant Playback device / the TV, where it ends up when the
        // laptop started it from there. Music that already plays in a room stays where it is.
        var atTheDesk = spotify?.State == "playing" && DeskSources.Any(x => string.Equals(x, source, StringComparison.OrdinalIgnoreCase));
        if (!atTheDesk)
        {
            Logger.LogDebug("The laptop locked, the music is not at the desk (Spotify: {State}, source {Source})", spotify?.State, source);
            return;
        }

        Core.ActivityContext.Note("De laptop is vergrendeld: de muziek gaat door in de keuken");
        Logger.LogInformation("The laptop locked while the music plays on it: the music goes on in the kitchen");
        _onKitchenBecauseOfLaptop = true;
        SpotifyConnectTo(KitchenDevice);
    }

    private void LaptopUnlocked()
    {
        if (!_onKitchenBecauseOfLaptop) return;
        _onKitchenBecauseOfLaptop = false;
        if (!LaptopMusicMayMove) return;

        // Whatever speaker plays now (the music may have followed to another room), it goes back to the laptop
        if (PlayingRoom() == null && HaContext.GetState(SpotifyState)?.State != "playing") return;

        Core.ActivityContext.Note("De laptop is ontgrendeld: de muziek gaat terug naar de laptop");
        Logger.LogInformation("The laptop unlocked: the music goes back to the laptop");
        SpotifyConnectTo(LaptopDevice);
    }

    private static string? Attribute(EntityState? state, string name) =>
        state?.AttributesJson is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
