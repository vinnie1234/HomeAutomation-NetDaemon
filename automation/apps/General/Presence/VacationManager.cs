using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Presence;

/// <summary>
/// Vacation per person (<c>input_boolean.vakantie_vincent</c> and <c>vakantie_carleen</c>, on the page of each of them). A person on vacation has no work
/// and no routes (see <see cref="Models.Persons.WorkSchedule.IsOnLeave"/>). When both are on vacation the whole house is on vacation
/// (<c>input_boolean.onvacation</c>: curtains, presence simulation and so on); when one of them is back the house is no longer on vacation,
/// but only when this app switched it on: a vacation that came from the calendar or was set by hand stays.
/// </summary>
[NetDaemonApp(Id = nameof(VacationManager))]
public class VacationManager : BaseApp
{
    private bool _houseOnBecauseOfBoth;

    public VacationManager(
        IHaContext ha,
        ILogger<VacationManager> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        VincentSchedule.Vacation.StateChanges().Merge(CarleenSchedule.Vacation.StateChanges()).Subscribe(_ => Update());
    }

    private void Update()
    {
        var both = VincentSchedule.Vacation.IsOn() && CarleenSchedule.Vacation.IsOn();
        if (both && Entities.InputBoolean.Onvacation.IsOff())
        {
            Logger.LogInformation("Both are on vacation: the house is on vacation");
            _houseOnBecauseOfBoth = true;
            Entities.InputBoolean.Onvacation.TurnOn();
        }
        else if (!both && _houseOnBecauseOfBoth)
        {
            Logger.LogInformation("One of them is back: the house is no longer on vacation");
            _houseOnBecauseOfBoth = false;
            Entities.InputBoolean.Onvacation.TurnOff();
        }
    }
}
