using System.Diagnostics;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Enum;
using Automation.Helpers;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(FunApp))]
// ReSharper disable once UnusedType.Global
public class FunApp : BaseApp
{
    private readonly AppConfiguration _config;
    /// <summary>
    /// Initializes a new instance of the <see cref="FunApp"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="config">The application configuration.</param>
    public FunApp(IHaContext ha, ILogger<FunApp> logger, INotify notify, IScheduler scheduler,
                  IOptions<AppConfiguration> config)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        Friends();
        Parents();
        NewYear();
    }

    /// <summary>
    /// Sets up the actions to be taken when the friends button is pressed.
    /// </summary>
    private void Friends()
    {
        Entities.InputButton.StartFriends.StateChanges()
            .Subscribe(_ =>
            {
                Notify.SendMusicToHome("http://192.168.50.189:8123/local/Friends.mp3");
                Entities.Light.Hal.TurnOn();
                Entities.Switch.Bot29ff.TurnOn();
            });
    }

    /// <summary>
    /// Sets up the actions to be taken when the parents arrive home.
    /// </summary>
    private void Parents()
    {
        Entities.DeviceTracker.A52sVanEddy.StateChanges()
            .Where(x => x.Entity.State == "home")
            .Subscribe(_ => SendMessageParents());
        Entities.DeviceTracker.S20FeVanJannette.StateChanges()
            .Where(x => x.Entity.State == "home")
            .Subscribe(_ => SendMessageParents());
    }

    /// <summary>
    /// Sends a welcome message to the parents.
    /// </summary>
    private void SendMessageParents()
    {
        var houseState = Globals.GetHouseState(Entities);
        var message = houseState == HouseState.Morning ? "Goedemorgen Ed en Jannette, welkom bij Vincent!" : "Goedemiddag Ed en Jannette, Welkom bij Vincent";

        Notify.NotifyHouse("Parents", message, false, 300);
    }

    /// <summary>
    /// Schedules the actions to be taken on New Year's Eve and New Year's Day.
    /// </summary>
    private void StartNewYearOnNewYear()
    {
        Scheduler.ScheduleCron("10 58 23 31 12 *", () =>
        {
            Notify.SendMusicToHome("http://192.168.50.189:8123/local/HappyNewYear.mp3", 0.4);
        }, true);

        Scheduler.ScheduleCron("59 58 23 31 12 *", () =>
        {
            Entities.MediaPlayer.HeleHuis.VolumeSet(0.9);
        }, true);

        Scheduler.ScheduleCron("00 00 01 01 *", ChristmasFirework);
    }

    /// <summary>
    /// Sets up the actions to be taken when the New Year button is pressed or on New Year's Eve.
    /// </summary>
    private void NewYear()
    {
        StartNewYearOnNewYear();

        Entities.InputButton.Startnewyear.StateChanges().Subscribe( _ =>
        {
            Notify.SendMusicToHome("http://192.168.50.189:8123/local/HappyNewYear.mp3", 0.4);
            Thread.Sleep(_config.Timing.NewYearMusicDelay);
            Entities.MediaPlayer.HeleHuis.VolumeSet(0.9);
            ChristmasFirework();
        });
    }

    /// <summary>
    /// Simulates a Christmas firework display by changing the colors of the lights.
    /// </summary>
    private void ChristmasFirework()
    {
        var rnd = new Random();
        var s = new Stopwatch();
        s.Start();

        do
        {
            var num = rnd.Next(1, 6);

            switch (num)
            {
                case 1:
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Green);
                    Entities.Light.HuePlayMidden.TurnOn(rgbColor: LightColors.Green);
                    Entities.Light.HuePlayLinks.TurnOn(rgbColor: LightColors.Green);
                    Entities.Light.HuePlayRechts.TurnOn(rgbColor: LightColors.Green);
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Red);
                    break;
                case 2:
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Red);
                    Entities.Light.HuePlayMidden.TurnOn(rgbColor: LightColors.Red);
                    Entities.Light.HuePlayLinks.TurnOn(rgbColor: LightColors.Red);
                    Entities.Light.HuePlayRechts.TurnOn(rgbColor: LightColors.Red);
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Green);
                    break;
                case 3:
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Blue);
                    Entities.Light.HuePlayMidden.TurnOn(rgbColor: LightColors.Blue);
                    Entities.Light.HuePlayLinks.TurnOn(rgbColor: LightColors.Blue);
                    Entities.Light.HuePlayRechts.TurnOn(rgbColor: LightColors.Blue);
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Yellow);
                    break;
                case 4:
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.White);
                    Entities.Light.HuePlayMidden.TurnOn(rgbColor: LightColors.White);
                    Entities.Light.HuePlayLinks.TurnOn(rgbColor: LightColors.White);
                    Entities.Light.HuePlayRechts.TurnOn(rgbColor: LightColors.White);
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Blue);
                    break;
                case 5:
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.Yellow);
                    Entities.Light.HuePlayMidden.TurnOn(rgbColor: LightColors.Yellow);
                    Entities.Light.HuePlayLinks.TurnOn(rgbColor: LightColors.Yellow);
                    Entities.Light.HuePlayRechts.TurnOn(rgbColor: LightColors.Yellow);
                    Entities.Light.Tv.TurnOn(rgbColor: LightColors.White);
                    break;
            }

            Thread.Sleep(_config.Timing.ShortDelay);
        } while (s.Elapsed < TimeSpan.FromMinutes(4));

        Entities.MediaPlayer.HeleHuis.VolumeSet(0.4);
        Entities.Light.Tv.TurnOn(effect: "opal");
    }
}