using Automation.apps.Rooms.LivingRoom;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class GamingTests
{
    [Fact]
    public void GameSetUp_TurnsOnDevicesAndOffLights_WhenAutomationEnabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        
        ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("device_tracker.sony").Returns(new EntityState { State = "off" });
        
        var app = ctx.InitApp<Gaming>();

        // Act
        ctx.ChangeStateFor("device_tracker.sony").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("media_player", "turn_on", "tv");
        ctx.VerifyCallService("media_player", "select_source", "tv");
        ctx.VerifyCallService("media_player", "turn_on", "av_soundbar");
        ctx.VerifyCallService("light", "turn_off", "plafond_woonkamer");
        ctx.VerifyCallService("light", "turn_off", "plafond");
        ctx.VerifyCallService("light", "turn_off", "nachtkastje");
        ctx.VerifyCallService("light", "turn_off", "hue_filament_bulb_1");
        ctx.VerifyCallService("light", "turn_off", "hue_filament_bulb_2");
        ctx.VerifyCallService("media_player", "volume_set", "tv");
    }

    [Fact]
    public void GameSetUp_DoesNothing_WhenAutomationDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        
        ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { State = "on" });
        ctx.HaContext.GetState("device_tracker.sony").Returns(new EntityState { State = "off" });
        
        var app = ctx.InitApp<Gaming>();

        // Act
        ctx.ChangeStateFor("device_tracker.sony").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("media_player.turn_on");
        ctx.VerifyNotCallService("media_player.select_source");
        ctx.VerifyNotCallService("light.turn_off");
        ctx.VerifyNotCallService("media_player.volume_set");
    }
}

