using System.Reactive.Concurrency;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that translates Google Assistant button presses to other actions.
/// </summary>
[NetDaemonApp(Id = nameof(GoogleAssistantButtonTranslate))]
public class GoogleAssistantButtonTranslate : BaseApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleAssistantButtonTranslate"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public GoogleAssistantButtonTranslate(IHaContext haContext, ILogger<GoogleAssistantButtonTranslate> logger, INotify notify, IScheduler scheduler)
        : base(haContext, logger, notify, scheduler)
    {
        foreach (var translateEntity in TranslationInputButtonEntity())
            translateEntity.Key.WhenTurnsOn(_ =>
            {
                translateEntity.Key.TurnOff();
                translateEntity.Value.Press();
            });

        foreach (var translateEntity in TranslationButtonEntity())
            translateEntity.Key.WhenTurnsOn(_ =>
            {
                translateEntity.Key.TurnOff();
                translateEntity.Value.Press();
            });
    }

    /// <summary>
    /// Gets a dictionary mapping input boolean entities to input button entities.
    /// </summary>
    /// <returns>A dictionary of input boolean entities and their corresponding input button entities.</returns>
    private Dictionary<InputBooleanEntity, InputButtonEntity> TranslationInputButtonEntity()
    {
        return new Dictionary<InputBooleanEntity, InputButtonEntity>
        {
            { Entities.InputBoolean.StartFriends, Entities.InputButton.StartFriends },
            { Entities.InputBoolean.Restartnetdaemon, Entities.InputButton.Restartnetdaemon },
            { Entities.InputBoolean.Pixelgivenextfeedeary, Entities.InputButton.Pixelgivenextfeedeary },
            { Entities.InputBoolean.Emptypetsnowy, Entities.InputButton.Emptypetsnowy },
            { Entities.InputBoolean.Cleanpetsnowy, Entities.InputButton.Cleanpetsnowy },
            { Entities.InputBoolean.Feedcat, Entities.InputButton.Feedcat },
            { Entities.InputBoolean.StartPc, Entities.InputButton.StartPc }
        };
    }

    /// <summary>
    /// Gets a dictionary mapping input boolean entities to button entities.
    /// </summary>
    /// <returns>A dictionary of input boolean entities and their corresponding button entities.</returns>
    private Dictionary<InputBooleanEntity, ButtonEntity> TranslationButtonEntity()
    {
        return new Dictionary<InputBooleanEntity, ButtonEntity>
        {
            { Entities.InputBoolean.VincentPcAfsluiten, Entities.Button.VincentPcAfsluiten }
        };
    }
}