using Automation.Interfaces;
using Automation.apps.General;
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

    public CatTests()
    {
        _ctx = AppTestContext.NewWithScheduler();
        _notify = Substitute.For<INotify>();
        _logger = Substitute.For<ILogger<Cat>>();
        _config = Options.Create(new AppConfig { 
            BaseUrlHomeAssistant = "http://test", 
            Discord = new DiscordConfig { Pixel = "pixel_channel" },
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
        _ = new Cat(_ctx.HaContext, _logger, _notify, _ctx.Scheduler, _config);
    }

    [Fact]
    public void PetSnowyExcretion_WhenCountGoesUp_IncrementsPixelInitCounter()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_number_of_excretion").FromState("1").ToState("2");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("counter", "increment", "petsnowylitterboxpixelinit", times: 1);
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
    public void ButtonFeedCat_WhenPressed_FeedsCatAndUpdatesStats()
    {
        CreateApp();
        
        _ctx.ChangeStateFor("input_button.feedcat").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedalltime", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountmanualfeed", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastmanualfeed", times: 1);
        
        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
    }
    
    [Fact]
    public void AutoFeedCat_WhenTimeReached_FeedsCat()
    {
        CreateApp();
        
        // Set time to just before 08:00

        
        _ctx.AdvanceTimeBy(TimeSpan.FromHours(8).Ticks + TimeSpan.FromSeconds(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        
        // Assert
        _ctx.VerifyCallService("input_number", "set_value", "pixeltotalamountfeedday", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountautomationfeed", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastautomatedfeed", times: 1);
        
        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
            
        // Assert input_boolean was turned off
        _ctx.VerifyCallService("input_boolean", "turn_off", "pixelskipnextautofeed", times: 1);
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
    public void EarlyFeed_WhenTriggered_FeedsNextAmountAndSetsSkipBool()
    {
        CreateApp();
        
        // Let's set time to 07:00, next feed is 08:00 (15 amount)

        
        _ctx.ChangeStateFor("input_button.pixelgivenextfeedeary").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.VerifyCallService("input_boolean", "turn_on", "pixelskipnextautofeed", times: 1);
        _ctx.VerifyCallService("input_number", "set_value", "pixellastamountmanualfeed", times: 1);
        _ctx.VerifyCallService("input_datetime", "set_datetime", "pixellastmanualfeed", times: 1);
        
        _ctx.VerifyCallService("number", "set_value", "zedar_a600_voeden", times: 1);
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
    
    [Fact]
    public void FeedCat_WhenTuyaFails_NotifiesManualFeeding()
    {
        CreateApp();
        
        // Throw exception when setting the feeder amount
        _ctx.HaContext
            .When(x => x.CallService("number", "set_value", Arg.Any<ServiceTarget>(), Arg.Any<object>()))
            .Throw(new Exception("Tuya API Error"));

        _ctx.ChangeStateFor("input_button.feedcat").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _notify.Received(1).NotifyPhoneVincent(
            "Kat voeding gefaald",
            Arg.Is<string>(s => s.Contains("is mislukt")),
            true,
            Arg.Any<double?>());
    }
}



