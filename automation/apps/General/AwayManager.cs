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

    /// <summary>
    /// Gets the current home presence state. Useful for debugging and testing.
    /// </summary>
    public HomePresenceState CurrentState 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _currentState; 
            } 
        } 
    }

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
    /// Handles the event when Vincent comes home.
    /// </summary>
    private void VincentHomeHandler()
    {
        Entities.Person.VincentMaarschalkerweerd
            .StateChanges()
            .Where(x => x.Old?.State != "home" &&
                        x.New?.State == "home" &&
                        !Vincent.IsHome)
            .Subscribe(_ => Entities.InputBoolean.Away.TurnOff());
    }

    /// <summary>
    /// Sets up the triggers for handling away and home states using state machine pattern.
    /// </summary>
    private void TriggersHandler()
    {
        Entities.InputBoolean.Away.WhenTurnsOn(_ => TransitionToState(HomePresenceState.Away));
        Entities.InputBoolean.Away.WhenTurnsOff(_ => TransitionToState(HomePresenceState.Returning));
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
    /// </summary>
    private void ExecuteAwayActions()
    {
        if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek)
            && DateTimeOffset.Now.Hour < 9
            && Entities.InputBoolean.Holliday.IsOff())
            Notify.NotifyPhoneVincent("Werkse Vincent", "Succes op kantoor :)", false, 5);
        else
            Notify.NotifyPhoneVincent("Tot ziens", "Je laat je huis weer alleen :(", false, 5);

        Entities.Light.TurnAllOff();
        Entities.MediaPlayer.Tv.TurnOff();
        Entities.MediaPlayer.AvSoundbar.TurnOff();
    }

    /// <summary>
    /// Executes the complete welcome home sequence asynchronously.
    /// </summary>
    private async Task ExecuteWelcomeHomeSequenceAsync()
    {
        try
        {
            Logger.LogInformation("Starting welcome home sequence");
            
            var houseState = GetHouseState(Entities);
            
            // Immediate actions
            NotifyVincentPhone(houseState);
            SetLightScene(houseState);
            
            Scheduler.Schedule(_config.Timing.WelcomeHomeDelay, async () =>
            {
                var message = "Welkom thuis Vincent!";
                if (Entities.Sensor.ZedarFoodStorageStatus.State != "full")
                    message += " Het eten van Pixel is bijna op!";

                await Notify.NotifyHouse("welcomeHome", message, true);
            
                // Transition to final Home state
                TransitionToState(HomePresenceState.Home);
            
                Logger.LogInformation("Welcome home sequence completed");
            });
            
       
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
        }
    }

    /// <summary>
    /// Sets the light scene based on the current house state.
    /// </summary>
    /// <param name="houseState">The current state of the house.</param>
    private void SetLightScene(HouseState houseState)
    {
        switch (houseState)
        {
            case HouseState.Morning:
                Entities.Scene.Woonkamermorning.TurnOn();
                break;
            case HouseState.Day:
                Entities.Scene.Woonkamerday.TurnOn();
                break;
            case HouseState.Evening:
                Entities.Scene.Woonkamerevening.TurnOn();
                break;
            case HouseState.Night:
                Entities.Scene.Woonkamernight.TurnOn();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(houseState), $"{houseState} is not a valid house state!");
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
    /// </summary>
    private void AutoAway()
    {
        Entities.Sensor.ThuisSmS938bDistance.StateChanges()
            .WhenStateIsFor(x => x?.State > 300, TimeSpan.FromMinutes(5), Scheduler)
            .Subscribe(_ =>
            {
                if (Vincent.DirectionOfTravel is "away_from" or "stationary" &&
                    Entities.InputBoolean.Away.IsOff() && Entities.Zone.Boodschappen.IsOff()) 
                    Entities.InputBoolean.Away.TurnOn();
            });
    }
}