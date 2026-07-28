using Automation.apps.General;
using Automation.Configuration;
using Automation.Enum;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class FunAppTests
{
    [Fact]
    public void FriendsButton_TurnsOnLightsAndPlaysMusic()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfiguration());
        
        ctx.HaContext.GetState("input_button.start_friends").Returns(new EntityState { State = "unknown" });
        
        var app = ctx.InitApp<FunApp>(config);

        // Act
        ctx.ChangeStateFor("input_button.start_friends").FromState("unknown").ToState("2021-01-01T00:00:00+00:00");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("media_player", "play_media", "hele_huis");
        ctx.VerifyCallService("light", "turn_on", "hal");
        ctx.VerifyCallService("switch", "turn_on", "bot_29ff");
    }

    [Fact]
    public void ParentsArrive_A52sVanEddy_SendsWelcomeMessage()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfiguration());
        
        // Setup state for globals so house state is known (e.g. Morning)
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { State = "Morning" });
        ctx.HaContext.GetState("device_tracker.a52s_van_eddy").Returns(new EntityState { State = "not_home" });
        
        var app = ctx.InitApp<FunApp>(config);

        // Act
        ctx.ChangeStateFor("device_tracker.a52s_van_eddy").FromState("not_home").ToState("home");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallServiceWithData("tts", "cloud_say", null, new HomeAssistantGenerated.TtsCloudSayParameters { EntityId = "media_player.hele_huis", Message = "Goedemorgen Ed en Jannette, welkom bij Vincent!" });
    }
    
    [Fact]
    public void ParentsArrive_S20FeVanJannette_SendsWelcomeMessage()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfiguration());
        
        // Setup state for globals so house state is known (e.g. Day)
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { State = "Day" });
        ctx.HaContext.GetState("device_tracker.s20_fe_van_jannette").Returns(new EntityState { State = "not_home" });
        
        var app = ctx.InitApp<FunApp>(config);

        // Act
        ctx.ChangeStateFor("device_tracker.s20_fe_van_jannette").FromState("not_home").ToState("home");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallServiceWithData("tts", "cloud_say", null, new HomeAssistantGenerated.TtsCloudSayParameters { EntityId = "media_player.hele_huis", Message = "Goedemiddag Ed en Jannette, Welkom bij Vincent" });
    }
    
    // Not testing NewYear button because it contains a 4-minute blocking do-while loop with Stopwatch that cannot be mocked,
    // which would cause the test runner to freeze for 4 minutes.
}


