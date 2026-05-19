using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Enum;
using static Automation.Globals;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the "away" state and related notifications using a state machine pattern.
/// </summary>
[NetDaemonApp(Id = nameof(AwayManager))]
public class AwayManager : BaseApp
{
    private readonly AppConfiguration _config = new();
    private HomePresenceState _currentState = HomePresenceState.Home;
    private readonly object _stateLock = new();
    private bool commingHomeTriggerd = false;
    private IDisposable? _carleenWakeUpSchedule;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwayManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public AwayManager(
        IHaContext ha,
        ILogger<AwayManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        TriggersHandler();
        VincentHomeHandler();
        AutoAway();
    }


    /// <summary>
    /// Handles the event when Vincent or Carleen comes or goes home.
    /// </summary>
    private void VincentHomeHandler()
    {
        // Either person arriving home turns off Away
        Entities.Person.VincentMaarschalkerweerd
            .StateChanges()
            .Where(x => x.Old?.State != "home" &&
                        x.New?.State == "home" &&
                        Entities.InputBoolean.Away.IsOn())
            .Subscribe(_ => Entities.InputBoolean.Away.TurnOff());

        Entities.Person.Carleen
            .StateChanges()
            .Where(x => x.Old?.State != "home" &&
                        x.New?.State == "home" &&
                        Entities.InputBoolean.Away.IsOn())
            .Subscribe(_ => Entities.InputBoolean.Away.TurnOff());

        // Carleen leaves as last person → turn on Away
        Entities.Person.Carleen
            .StateChanges()
            .Where(x => x.Old?.State == "home" &&
                        x.New?.State != "home" &&
                        Entities.Person.VincentMaarschalkerweerd.State != "home" &&
                        Entities.InputBoolean.Away.IsOff())
            .Subscribe(_ => Entities.InputBoolean.Away.TurnOn());
    }

    /// <summary>
    /// Sets up the triggers for handling away and home states using state machine pattern.
    /// </summary>
    private void TriggersHandler()
    {
        Entities.InputBoolean.Away.WhenTurnsOn(_ => TransitionToState(HomePresenceState.Away));
        Entities.InputBoolean.Away.WhenTurnsOff(_ =>
        {
            // If Away was suppressed because Carleen is home sleeping, don't start welcome-home sequence
            if (!Vincent.IsHome)
            {
                Logger.LogInformation("Away turned off but Vincent is not home (Carleen home scenario) — skipping Returning state");
                return;
            }
            TransitionToState(HomePresenceState.Returning);
        });
        Entities.BinarySensor.GangMotion.WhenTurnsOn(_ => HandleMotionDetected());
    }

    /// <summary>
    /// Thread-safe state transition method with logging and validation.
    /// </summary>
    /// <param name="newState">The target state to transition to.</param>
    private void TransitionToState(HomePresenceState newState)
    {
     
        lock (_stateLock)
        {
            var oldState = _currentState;
            
            // Validate state transition
            if (!IsValidStateTransition(oldState, newState))
            {
                Logger.LogWarning("Invalid state transition attempted: {OldState} → {NewState}", oldState, newState);
                return;
            }

            _currentState = newState;
            Logger.LogInformation("State transition: {OldState} → {NewState}", oldState, newState);
            
            // Execute state-specific actions
            ExecuteStateActions(newState);
        }
    }

    /// <summary>
    /// Validates if a state transition is allowed.
    /// </summary>
    /// <param name="from">Current state.</param>
    /// <param name="to">Target state.</param>
    /// <returns>True if transition is valid.</returns>
    private static bool IsValidStateTransition(HomePresenceState from, HomePresenceState to)
    {
        return (from, to) switch
        {
            // Same state is always valid (idempotent)
            _ when from == to => true,
            // Valid transitions
            (_, HomePresenceState.Away) => true, // Can always go away
            (HomePresenceState.Away, HomePresenceState.Returning) => true,
            (HomePresenceState.Returning, HomePresenceState.WelcomingHome) => true,
            (HomePresenceState.WelcomingHome, HomePresenceState.Home) => true,
            // All other transitions are invalid
            _ => false
        };
    }

    /// <summary>
    /// Executes actions specific to entering a new state.
    /// </summary>
    /// <param name="state">The state that was just entered.</param>
    private void ExecuteStateActions(HomePresenceState state)
    {
        switch (state)
        {
            case HomePresenceState.Away:
                ExecuteAwayActions();
                break;
            case HomePresenceState.Returning:
                Logger.LogInformation("Vincent is returning home, waiting for motion detection");
                break;
            case HomePresenceState.WelcomingHome:
                // Start welcome home sequence asynchronously
                _ = Task.Run(ExecuteWelcomeHomeSequenceAsync);
                break;
            case HomePresenceState.Home:
                Logger.LogInformation("Vincent is home, normal automation active");
                break;
        }
    }

    /// <summary>
    /// Handles motion detection based on current state.
    /// </summary>
    private void HandleMotionDetected()
    {
        lock (_stateLock)
        {
            // Only trigger welcome home if we're in Returning state
            if (_currentState == HomePresenceState.Returning)
            {
                Logger.LogInformation("Motion detected while returning - starting welcome home sequence");
                _currentState = HomePresenceState.WelcomingHome;
                _ = Task.Run(ExecuteWelcomeHomeSequenceAsync);
            }
            else
            {
                Logger.LogDebug("Motion detected in state {CurrentState} - no action taken", _currentState);
            }
        }
    }

    /// <summary>
    /// Executes the away actions when leaving home.
    /// When Carleen is home and sleeping (Vincent left for office), Away is immediately
    /// cancelled and a 09:00 wake-up timer is scheduled for Carleen instead.
    /// </summary>
    private void ExecuteAwayActions()
    {
        if (Carleen.IsHome && Carleen.IsSleeping)
        {
            Logger.LogInformation("Away triggered but Carleen is home and sleeping — suppressing away actions");
            Entities.InputBoolean.Away.TurnOff();
            ScheduleCarleenWakeUp();
            return;
        }

        if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek)
            && DateTimeOffset.Now.Hour < 9
            && Entities.InputBoolean.Holliday.IsOff())
            Notify.NotifyPhoneVincent("Werkse Vincent", "Succes op kantoor :)", false, 5);
        else
            Notify.NotifyPhoneVincent("Tot ziens", "Je laat je huis weer alleen :(", false, 5);

        if (!Carleen.IsHome)
        {
            Entities.Light.TurnAllOff();
            Entities.MediaPlayer.Tv.TurnOff();
            Entities.MediaPlayer.AvSoundbar.TurnOff();
        }
    }

    /// <summary>
    /// Schedules Carleen's sleeping boolean to turn off at 09:00.
    /// Cancels any previously scheduled wake-up.
    /// </summary>
    private void ScheduleCarleenWakeUp()
    {
        _carleenWakeUpSchedule?.Dispose();

        var now = DateTimeOffset.Now;
        var wakeUpTime = now.Date.AddHours(9);
        if (wakeUpTime <= now)
            wakeUpTime = wakeUpTime.AddDays(1);

        var delay = wakeUpTime - now;
        _carleenWakeUpSchedule = Scheduler.Schedule(delay, () =>
        {
            if (Carleen.IsHome && Carleen.IsSleeping)
            {
                Logger.LogInformation("Scheduled 09:00 wake-up: setting Carleen sleeping off");
                Entities.InputBoolean.Sleepingcarleen.TurnOff();
            }
        });

        Logger.LogInformation("Scheduled Carleen wake-up at 09:00 (in {Delay})", delay);
    }

    /// <summary>
    /// Executes the complete welcome home sequence asynchronously.
    /// </summary>
    private Task ExecuteWelcomeHomeSequenceAsync()
    {
        try
        {
            Logger.LogInformation("Starting welcome home sequence");
            
            var houseState = GetHouseState(Entities);
            
            // Immediate actions
            NotifyVincentPhone(houseState);
            LightExtension.SetLightSceneWoonkamer(Entities);
            
            Scheduler.Schedule(_config.Timing.WelcomeHomeDelay, () =>
            {
                var message = "";

                var vincentHome = Entities.Person.VincentMaarschalkerweerd.State == "home";
                var carleenHome = Entities.Person.Carleen.State == "home" ||
                                  Entities.DeviceTracker.CarleenMobiel.State == "home";
                
                if (vincentHome && carleenHome)
                {
                    message += "Welkom thuis Vincent en Carleen!";
                }else if (carleenHome)
                {
                    message += "Welkom thuis Carleen!";
                }else
                {
                    message = "Welkom thuis Vincent!";
                }
                
                if (Entities.Sensor.ZedarFoodStorageStatus.State != "full")
                    message += " Het eten van Pixel is bijna op!";

                Notify.NotifyHouse("welcomeHome", message, true);
            
                // Transition to final Home state
                TransitionToState(HomePresenceState.Home);
            
                Logger.LogInformation("Welcome home sequence completed");
            });
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during welcome home sequence");
            
            // Reset to safe state on error
            lock (_stateLock)
            {
                _currentState = HomePresenceState.Home;
                Logger.LogWarning("Reset to Home state due to error");
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sends a context-aware notification to Vincent when he comes home.
    /// </summary>
    /// <param name="houseState">The current state of the house.</param>
    private void NotifyVincentPhone(HouseState houseState)
    {
        var greeting = houseState switch
        {
            HouseState.Morning => "Goedemorgen Vincent!",
            HouseState.Day => "Welkom thuis!",
            HouseState.Evening => "Goedenavond Vincent!",
            HouseState.Night => "Welkom thuis (stil aan, het is laat!)",
            _ => "Welkom thuis Vincent!"
        };
        
        Notify.NotifyPhoneVincent("Thuis", greeting, canAlwaysSendNotification: true);
    }
    
    /// <summary>
    /// Automatically sets the "away" state based on Vincent's phone distance and direction of travel.
    /// Skipped when Carleen is home.
    /// </summary>
    private void AutoAway()
    {
        Entities.Sensor.ThuisSmS938bDistance.StateChanges()
            .WhenStateIsFor(x => x?.State > 300, TimeSpan.FromMinutes(5), Scheduler)
            .Subscribe(_ =>
            {
                if (Vincent.DirectionOfTravel is "away_from" or "stationary" &&
                    Entities.InputBoolean.Away.IsOff() &&
                    Entities.Zone.Boodschappen.IsOff() &&
                    !Carleen.IsHome)
                    Entities.InputBoolean.Away.TurnOn();
            });
    }
}