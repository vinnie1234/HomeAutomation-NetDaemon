using System.Reactive.Concurrency;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the PC and its related actions.
/// </summary>
[NetDaemonApp(Id = nameof(PcManager))]
public class PcManager : BaseApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PcManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public PcManager(
        IHaContext ha,
        ILogger<PcManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        Entities.InputButton.StartPc
            .StateChanges()
            .Subscribe(_ =>
            {
                Entities.Light.Bureau.TurnOn();
                Entities.Light.Nachtkastje.TurnOff();
                Entities.Light.Plafond.TurnOn();
                Entities.MediaPlayer.Tv.TurnOff();
            });

        Entities.Sensor.VincentPcLaatstopgestart
            .StateChanges()
            .Subscribe(_ =>
            {
                Entities.Light.Bureau.TurnOn();
                Entities.Light.Nachtkastje.TurnOff();
                Entities.Light.Plafond.TurnOn();
                Entities.MediaPlayer.Tv.TurnOff();
            });

        // Entities.Button.VincentPcAfsluiten
        //     .StateChanges()
        //     .Subscribe(_ =>
        //     {
        //         LightExtension.TurnOnLightsWoonkamer(Entities, scheduler);
        //
        //         Thread.Sleep(TimeSpan.FromMinutes(1));
        //         Entities.Light.Bureau.TurnOff();
        //     });
    }
}