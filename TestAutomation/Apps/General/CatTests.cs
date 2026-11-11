using Automation.Interfaces;
using Automation.apps.General.Pets;
using Automation.Configuration;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class CatTests
{
    private readonly AppTestContext _ctx;
    private readonly INotify _notify;
    private readonly ILogger<Cat> _logger;
    private readonly IOptions<AppConfig> _config;
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();

    public CatTests()
    {
        _ctx = AppTestContext.NewWithScheduler();
        _notify = Substitute.For<INotify>();
        _logger = Substitute.For<ILogger<Cat>>();
        _config = Options.Create(new AppConfig { 
            BaseUrlHomeAssistant = "http://test", 
            Discord = new DiscordConfig { Pixel = "pixel_channel" }
        });

        // Setup feed times
        _ctx.HaContext.GetState("input_datetime.pixelfeedfirsttime").Returns(new EntityState { EntityId = "input_datetime.pixelfeedfirsttime", State = "08:00:00" });
        _ctx.HaContext.GetState("input_number.pixelfeedfirstamount").Returns(new EntityState { EntityId = "input_number.pixelfeedfirstamount", State = "15" });
        
        _ctx.HaContext.GetState("input_datetime.pixelfeedsecondtime").Returns(new EntityState { EntityId = "input_datetime.pixelfeedsecondtime", State = "12:00:00" });
        _ctx.HaContext.GetState("input_number.pixelfeedsecondamount").Returns(new EntityState { EntityId = "input_number.pixelfeedsecondamount", State = "15" });
        
        _ctx.HaContext.GetState("input_datetime.pixelfeedthirdtime").Returns(new EntityState { EntityId = "input_datetime.pixelfeedthirdtime", State = "18:00:00" });
        _ctx.HaContext.GetState("input_number.pixelfeedthirdamount").Returns(new EntityState { EntityId = "input_number.pixelfeedthirdamount", State = "15" });
        
        _ctx.HaContext.GetState("input_datetime.pixelfeedfourthtime").Returns(new EntityState { EntityId = "input_datetime.pixelfeedfourthtime", State = "22:00:00" });
        _ctx.HaContext.GetState("input_number.pixelfeedfourthamount").Returns(new EntityState { EntityId = "input_number.pixelfeedfourthamount", State = "15" });

        // Setup other entities
        _ctx.HaContext.GetState("input_boolean.pixelskipnextautofeed").Returns(new EntityState { EntityId = "input_boolean.pixelskipnextautofeed", State = "off" });
        _ctx.HaContext.GetState("input_number.pixeltotalamountfeedday").Returns(new EntityState { EntityId = "input_number.pixeltotalamountfeedday", State = "0" });
        _ctx.HaContext.GetState("input_number.pixeltotalamountfeedalltime").Returns(new EntityState { EntityId = "input_number.pixeltotalamountfeedalltime", State = "100" });
        _ctx.HaContext.GetState("input_number.pixelnumberofmanualfood").Returns(new EntityState { EntityId = "input_number.pixelnumberofmanualfood", State = "5" });
        _ctx.HaContext.GetState("input_number.pixellastamountmanualfeed").Returns(new EntityState { EntityId = "input_number.pixellastamountmanualfeed", State = "0" });
    }

    private void CreateApp()
    {
        _ = new Cat(_ctx.HaContext, _logger, _notify, _ctx.Scheduler, _config, _storage, _entityManager);
    }

    [Fact]
    public void PetSnowyStatus_WhenPetInto_IncrementsPixelInitCounter()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_status").FromState("standby").ToState("pet_into");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("counter", "increment", "petsnowylitterboxpixelinit", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastlitterboxvisit", times: 1);
    }
    
    [Fact]
    public void PetSnowyStatus_WhenCleaning_IncrementsCleaningCounter()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_status").FromState("standby").ToState("cleaning");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("counter", "increment", "petsnowylittleboxcleaning", times: 1);
    }
    
    [Fact]
    public void PetSnowyEmptied_WhenNotificationComes_IncrementsEmptyingCounter()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("binary_sensor.snow_self_cleaning_litter_box_notification_empty_complete").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("counter", "increment", "petsnowylitterboxemptying", times: 1);
    }

    [Fact]
    public void AutoFeedCat_WhenSkipNextAutoFeedIsTrue_SkipsFeedingAndTurnsOffBool()
    {
        _ctx.HaContext.GetState("input_boolean.pixelskipnextautofeed").Returns(new EntityState { EntityId = "input_boolean.pixelskipnextautofeed", State = "on" });
        CreateApp();
        
        // Set time to just before 08:00

        
        _ctx.AdvanceTimeBy(TimeSpan.FromHours(8).Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        
        // Assert no feeding occurred
        _ctx.VerifyNotCallService("number.set_value");
        
        // Assert input_boolean was turned off anyway
        _ctx.VerifyCallService("input_boolean", "turn_off", "pixelskipnextautofeed", times: 1);
    }

    [Fact]
    public void CleanPetSnowy_WhenTriggered_SendsCleanCommand()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("input_button.cleanpetsnowy").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("button", "press", "snow_self_cleaning_litter_box_manual_clean", times: 1);
    }

    [Fact]
    public void EmptyPetSnowy_WhenTriggered_SendsEmptyCommand()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("input_button.emptypetsnowy").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("button", "press", "snow_self_cleaning_litter_box_empty_litter_box", times: 1);
    }
    
    [Fact]
    public void FountainOff_TriggersAlarm()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("switch.snow_automatic_pet_water_fountain_schakelaar").FromState("on").ToState("off");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        // Wait 600 seconds as specified in WhenTurnsOff(_ => ..., 600)
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(601).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _notify.Received(1).NotifyDiscord(
            "", 
            Arg.Is<string[]>(t => t.AsEnumerable().Contains("pixel_channel")), 
            Arg.Is<Automation.Models.DiscordNotificationModels.DiscordNotificationModel>(m => m.Embed!.Title!.Contains("FOUNTAIN STAAT UIT")));
    }
    
    [Fact]
    public void LitterBoxOff_TriggersAlarm()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("switch.snow_self_cleaning_litter_box_auto_clean").FromState("on").ToState("off");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(601).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _notify.Received(1).NotifyDiscord(
            "", 
            Arg.Is<string[]>(t => t.AsEnumerable().Contains("pixel_channel")), 
            Arg.Is<Automation.Models.DiscordNotificationModels.DiscordNotificationModel>(m => m.Embed!.Title!.Contains("KATTENBAK STAAT UIT")));
    }
    
    [Fact]
    public void MidnightCron_ResetsDailyTotalFeed()
    {
        CreateApp();
        
        // "59 23 * * *" -> 23:59:00

        _ctx.AdvanceTimeBy(TimeSpan.FromHours(25).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        
        var calls = _ctx.HaContext.ReceivedCalls()
            .Where(x => x.GetMethodInfo().Name == "CallService")
            .Where(x => (string)x.GetArguments()[0]! == "input_number" && (string)x.GetArguments()[1]! == "set_value")
            .Where(x => x.GetArguments()[3] is InputNumberSetValueParameters { Value: 0 })
            .ToList();
        Assert.Single(calls);
    }
    
    [Fact]
    public void MonitorCat_ManualFeed_SendsDiscordNotification()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("input_datetime.pixellastmanualfeed").FromState("2023-01-01 10:00:00").ToState("2023-01-01 12:00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _notify.Received(1).NotifyDiscord(
            "", 
            Arg.Is<string[]>(t => t.AsEnumerable().Contains("pixel_channel")), 
            Arg.Is<Automation.Models.DiscordNotificationModels.DiscordNotificationModel>(m => m.Embed!.Title == "Pixel heeft handmatig eten gehad"));
    }
    
    [Fact]
    public void MonitorCat_AutomatedFeed_SendsDiscordNotification()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("input_datetime.pixellastautomatedfeed").FromState("2023-01-01 10:00:00").ToState("2023-01-01 12:00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _notify.Received(1).NotifyDiscord(
            "", 
            Arg.Is<string[]>(t => t.AsEnumerable().Contains("pixel_channel")), 
            Arg.Is<Automation.Models.DiscordNotificationModels.DiscordNotificationModel>(m => m.Embed!.Title == "Pixel heeft eten gehad"));
    }

    /// <summary>The feeder says it is feeding: what it does when it dispenses.</summary>
    private void FeederReports()
    {
        _ctx.ChangeStateFor("sensor.zedar_a600_feed_state").FromState("standby").ToState("feeding");
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void PassTheConfirmTime()
    {
        _ctx.AdvanceTimeBy(Cat.FeedConfirmTimeout.Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void PressManualFeed()
    {
        _ctx.ChangeStateFor("input_button.feedcat").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private void NotFedNotification(int times) =>
        _notify.Received(times).NotifyPhoneVincentCarleen("Pixel is NIET gevoerd", Arg.Any<string>(), true, Arg.Any<double?>(),
            Arg.Is<List<Automation.Models.ActionModel>?>(a => a != null && a.Count == 1 && a[0].Title.StartsWith("Alsnog voeren")),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Cat.PixelPage);

    [Fact]
    public void ButtonFeedCat_AsksTheFeeder_ButCountsNothingUntilTheFeederConfirms()
    {
        CreateApp();

        PressManualFeed();

        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 0);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastmanualfeed", times: 0);

        FeederReports();

        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedalltime", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountmanualfeed", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastmanualfeed", times: 1);
        NotFedNotification(0);
    }

    [Fact]
    public void AutoFeedCat_WhenTimeReached_AsksTheFeeder_AndCountsAfterTheConfirmation()
    {
        CreateApp();

        _ctx.AdvanceTimeBy(TimeSpan.FromHours(7).Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 0);
        _ctx.VerifyCallService("input_boolean", "turn_off", "pixelskipnextautofeed", times: 1);

        FeederReports();

        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountautomationfeed", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastautomatedfeed", times: 1);
    }

    [Fact]
    public void AutoFeedCat_WhenTheFeederDoesNotConfirm_SaysSo_AndCountsNothing()
    {
        CreateApp();

        _ctx.AdvanceTimeBy(TimeSpan.FromHours(7).Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        PassTheConfirmTime();

        NotFedNotification(1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 0);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastautomatedfeed", times: 0);
        // No second try on its own
        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
        // The sensor for the dashboard has what was missed: 15 portions at 08:00
        _entityManager.Received().SetState(Cat.MissedSensor, 15, Arg.Any<object?>());
        _storage.Received().Save(Cat.MissedStorageKey, Arg.Is<List<Cat.MissedFeed>>(l => l.Count == 1 && l[0].Time == "08:00" && l[0].Amount == 15));
    }

    [Fact]
    public void WhenTheFeederConfirmsLate_NothingIsMissed()
    {
        CreateApp();
        PressManualFeed();

        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(20).Ticks);
        FeederReports();
        PassTheConfirmTime();

        NotFedNotification(0);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 1);
    }

    [Fact]
    public void WhenAskingTheFeederFails_ItIsReportedAtOnce()
    {
        CreateApp();
        _ctx.HaContext
            .When(x => x.CallService("number", "set_value", Arg.Any<ServiceTarget>(), Arg.Any<object>()))
            .Throw(new Exception("Tuya API Error"));

        PressManualFeed();

        NotFedNotification(1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 0);
    }

    [Fact]
    public void FeedingWhatWasMissed_GivesExactlyThatAmount_AndTurnsItIntoFed()
    {
        CreateApp();
        _ctx.AdvanceTimeBy(TimeSpan.FromHours(7).Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        PassTheConfirmTime();   // 08:00, 15 portions missed
        _ctx.HaContext.ClearReceivedCalls();
        _entityManager.ClearReceivedCalls();

        _ctx.ChangeStateFor(Cat.CatchUpButton).FromState("unknown").ToState("2026-11-11T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.Received(1).CallService("number", "set_value", Arg.Any<ServiceTarget?>(),
            Arg.Is<object?>(d => System.Text.Json.JsonSerializer.Serialize(d, (System.Text.Json.JsonSerializerOptions?)null).Contains("15")));
        _entityManager.DidNotReceive().SetState(Cat.MissedSensor, 0, Arg.Any<object?>());

        FeederReports();

        _entityManager.Received().SetState(Cat.MissedSensor, 0, Arg.Any<object?>());
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountmanualfeed", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 1);
    }

    [Fact]
    public void FeedingWhatWasMissed_WhenItFailsAgain_SaysSo_AndKeepsWhatWasMissed()
    {
        CreateApp();
        _ctx.AdvanceTimeBy(TimeSpan.FromHours(7).Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        PassTheConfirmTime();

        _entityManager.ClearReceivedCalls();
        _ctx.ChangeStateFor(Cat.CatchUpButton).FromState("unknown").ToState("2026-11-11T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        PassTheConfirmTime();

        _notify.Received(1).NotifyPhoneVincentCarleen("Alsnog voeren is OOK mislukt", Arg.Any<string>(), true, Arg.Any<double?>(),
            Arg.Any<List<Automation.Models.ActionModel>?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Cat.PixelPage);
        _entityManager.DidNotReceive().SetState(Cat.MissedSensor, 0, Arg.Any<object?>());
    }

    [Fact]
    public void FeedingWhatWasMissed_WhenNothingWasMissed_DoesNothing()
    {
        CreateApp();

        _ctx.ChangeStateFor(Cat.CatchUpButton).FromState("unknown").ToState("2026-11-11T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 0);
    }

    [Fact]
    public void EarlyFeed_AsksTheFeeder_AndSetsTheSkipBool()
    {
        CreateApp();

        _ctx.ChangeStateFor("input_button.pixelgivenextfeedeary").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.VerifyCallService("input_boolean", "turn_on", "pixelskipnextautofeed", times: 1);
        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
        FeederReports();
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountmanualfeed", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastmanualfeed", times: 1);
    }

    [Fact]
    public void EarlyFeed_WhenItFails_TheScheduledFeedingStays()
    {
        CreateApp();

        _ctx.ChangeStateFor("input_button.pixelgivenextfeedeary").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        PassTheConfirmTime();

        NotFedNotification(1);
        _ctx.VerifyCallService("input_boolean", "turn_off", "pixelskipnextautofeed", times: 1);
    }

}
