using System.Reactive;
using System.Reactive.Concurrency;
using System.Text.Json;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.LivingRoom;

/// <summary>
/// Keeps the Samsung soundbar (HW-Q950A, through SmartThings: <c>media_player.soundbar</c>) on the right input and sets its volume once
/// when it turns on.
/// </summary>
/// <remarks>
/// Nothing is done to a soundbar that is on already: it was set by hand then. The input and the volume are only chosen when it turns on
/// (<see cref="FreshPowerOn"/>), whether it woke by itself over HDMI-CEC or was turned on here.
/// The TV (its own apps and tuner) plays through the soundbar over ARC (<see cref="TvInput"/>). The PS5 and the Google TV streamer are
/// connected to the soundbar itself (<see cref="ExternalInput"/>); the TV then shows them on its HDMI input.
/// <c>media_player.av_soundbar</c> is the Google Cast side of the same soundbar and is never touched here: turning it on launches a cast
/// receiver, which moves the soundbar to its Wi-Fi input and, when that ends, back to whatever input it used before.
/// </remarks>
[NetDaemonApp(Id = nameof(Soundbar))]
public class Soundbar : BaseApp
{
    /// <summary>The input of the TV over ARC: "D.IN / TV ARC" in the SmartThings app.</summary>
    public const string TvInput = "digital";

    /// <summary>The input of the PS5 and the Google TV streamer: "Externe apparaat (HDMI 1)" in the SmartThings app.</summary>
    public const string ExternalInput = "hdmi1";

    /// <summary>The volume the soundbar gets, once, when it turns on.</summary>
    public const double StartVolume = 0.10;

    /// <summary>How long after the soundbar turned on its volume is set and the input is chosen (a command at the very moment it starts is lost).</summary>
    public static readonly TimeSpan PowerOnDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long after the soundbar turned on it still counts as the same wake-up: HDMI-CEC turns it on a moment before the TV or the PS5 is
    /// reported as on. Later it was on already, so someone set it by hand.
    /// </summary>
    public static readonly TimeSpan FreshPowerOn = TimeSpan.FromSeconds(30);

    /// <summary>How long the same input is not asked for again: the TV and its SmartThings entity both report that it turned on, a moment apart.</summary>
    public static readonly TimeSpan RepeatGuard = TimeSpan.FromSeconds(5);

    /// <summary>How long after asking for an input the soundbar is checked: SmartThings reports the new input only after a while.</summary>
    public static readonly TimeSpan VerifyDelay = TimeSpan.FromSeconds(10);

    private DateTimeOffset _poweredOnAt = DateTimeOffset.MinValue;
    private string? _lastAsked;
    private DateTimeOffset _lastAskedAt;
    private DateTimeOffset _lastTurnOnAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets a value indicating whether the living room automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationlivingroom.IsOn();

    /// <summary>
    /// Initializes a new instance of the <see cref="Soundbar"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="config">The application settings.</param>
    public Soundbar(
        IHaContext ha,
        ILogger<Soundbar> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        // Only off -> on counts as turning on: unavailable -> on is NetDaemon or Home Assistant restarting while someone is listening
        Entities.MediaPlayer.Soundbar.StateChanges()
            .Where(x => x.Old?.State == "off" && x.New?.State == "on")
            .Do(_ => _poweredOnAt = Scheduler.Now)
            .Select(_ => Observable.Timer(PowerOnDelay, Scheduler))
            .Switch()
            .Subscribe(_ => PoweredOn());

        // The TV turns on, the TV changes input (its SmartThings entity reports it as "source": digital_tv or hdmi3) or the PS5 turns on:
        // the input is chosen at once, and again if the situation changes a moment later (the TV and PS5 wake each other over HDMI-CEC)
        Observable.Merge(
                Entities.MediaPlayer.Tv.StateChanges()
                    .Where(x => !Tv.IsTvOnState(x.Old?.State) && Tv.IsTvOnState(x.New?.State))
                    .Select(_ => true),
                // Another input of a TV that is on does not turn on a soundbar that someone turned off
                HaContext.StateAllChanges()
                    .Where(x => x.Entity.EntityId == Entities.MediaPlayer.GoogleTv.EntityId)
                    .Where(x => ChangedInput(x.Old, x.New))
                    .Select(x => x.Old?.State != "on" && x.New?.State == "on"),
                Entities.DeviceTracker.Sony.StateChanges()
                    .Where(x => x.Old?.State == "not_home" && x.New?.State == "home")
                    .Select(_ => true))
            .Subscribe(ChooseInput);
    }

    /// <summary>
    /// Works out which input the soundbar should be on now and asks for it, but only when it was just turned on (a soundbar that is on
    /// already was set by hand). Nothing is chosen when the situation is unclear.
    /// </summary>
    /// <param name="allowTurnOn">Whether a soundbar that is off may be turned on (the TV or the PS5 turned on, not a mere change of input).</param>
    private void ChooseInput(bool allowTurnOn)
    {
        if (DisableLightAutomations) return;

        // The SmartThings entity of the TV reports it turned on a few seconds before the TV itself does
        var tvOn = Tv.IsTvOnState(Entities.MediaPlayer.Tv.State) || Entities.MediaPlayer.GoogleTv.State == "on";
        var tvSource = tvOn ? TextAttribute(Entities.MediaPlayer.GoogleTv.EntityState, "source") : null;
        string? input = null;
        if (!string.IsNullOrEmpty(tvSource))
            input = tvSource.StartsWith("hdmi", StringComparison.OrdinalIgnoreCase) ? ExternalInput : TvInput;
        else if (Entities.DeviceTracker.Sony.State == "home")
            input = ExternalInput;

        if (input is null) return;

        switch (Entities.MediaPlayer.Soundbar.State)
        {
            case "off" when allowTurnOn:
                // The input is chosen again once it is on (PoweredOn)
                if (Scheduler.Now - _lastTurnOnAt >= RepeatGuard)
                {
                    _lastTurnOnAt = Scheduler.Now;
                    Entities.MediaPlayer.Soundbar.TurnOn();
                }

                break;
            case "on" when Scheduler.Now - _poweredOnAt <= FreshPowerOn:
                SelectInput(input);
                break;
            case "on":
                Logger.LogDebug("Soundbar was on already: left as it is");
                break;
        }
    }

    /// <summary>
    /// The soundbar turned on: its volume is set (this is the only place that does), and the input is chosen.
    /// </summary>
    private void PoweredOn()
    {
        if (DisableLightAutomations) return;

        Entities.MediaPlayer.Soundbar.VolumeSet(StartVolume);
        ChooseInput(allowTurnOn: false);
    }

    /// <summary>
    /// Selects the input unless the soundbar is on it already, and logs when it did not switch.
    /// </summary>
    /// <param name="input">The input to select.</param>
    private void SelectInput(string input)
    {
        if (CurrentInput == input) return;
        if (_lastAsked == input && Scheduler.Now - _lastAskedAt < RepeatGuard) return;

        _lastAsked = input;
        _lastAskedAt = Scheduler.Now;
        Logger.LogInformation("Soundbar from {From} to {To}", CurrentInput ?? "unknown", input);
        Entities.MediaPlayer.Soundbar.SelectSource(input);
        Observable.Timer(VerifyDelay, Scheduler)
            .Subscribe(_ =>
            {
                if (CurrentInput != input)
                    Logger.LogWarning("Soundbar did not switch to {Input} (it is on {Current}): does media_player.select_source work for this soundbar?", input, CurrentInput ?? "unknown");
            });
    }

    /// <summary>
    /// Whether the TV (its SmartThings entity) turned on or off or switched to another input. Other attributes (volume, ...) change all the
    /// time and are no reason to choose the soundbar input again, which would undo a choice made in the SmartThings app.
    /// </summary>
    /// <param name="old">The state before the change.</param>
    /// <param name="new">The state after the change.</param>
    /// <returns>True when the state or the "source" attribute differs.</returns>
    public static bool ChangedInput(EntityState? old, EntityState? @new) =>
        old?.State != @new?.State || TextAttribute(old, "source") != TextAttribute(@new, "source");

    /// <summary>
    /// Gets the input the soundbar reports now, or null when it reports none.
    /// </summary>
    private string? CurrentInput => TextAttribute(Entities.MediaPlayer.Soundbar.EntityState, "source");

    /// <summary>
    /// Reads a text attribute of an entity state.
    /// </summary>
    /// <param name="state">The entity state, or null.</param>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>The text, or null when the state or the attribute is missing or is not text.</returns>
    private static string? TextAttribute(EntityState? state, string name) =>
        state?.AttributesJson is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
