using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class SleepManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldTurnOffAllLightsWhenGoingToSleep()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablelightautomationgeneral", "off");
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("input_boolean.sleeping")
            .FromState("off")
            .ToState("on");

        // Assert — should turn off all lights when going to sleep.
        _ctx.HaContext.Received().CallService("light", "turn_all_off", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldTurnOffTvAndCloseBlindWhenGoingToSleep()
    {
        // Arrange
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("input_boolean.sleeping")
            .FromState("off")
            .ToState("on");

        // Assert — should turn off TV and close roller blind.
        _ctx.HaContext.Received().CallService("media_player", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("media_player.tv")), 
            Arg.Any<object>());
        _ctx.HaContext.Received().CallService("cover", "set_cover_position", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("cover.rollerblind_0003")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldNotifyAboutGarbageWhenGoingToSleep()
    {
        // Arrange
        _ctx.WithEntityState("sensor.afval_morgen", "Papier");
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("input_boolean.sleeping")
            .FromState("off")
            .ToState("on");

        // Assert — should notify about garbage collection.
        _ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone");
    }

    [Fact]
    public void ShouldOpenBlindOnWeekendWakeUp()
    {
        // Arrange
        _ctx.SetCurrentTime(new DateTime(2024, 1, 6, 8, 0, 0)); // Saturday
        _ctx.WithEntityState("input_boolean.onvacation", "off");
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("input_boolean.sleeping")
            .FromState("on")
            .ToState("off");

        // Assert — should open blind fully on weekend.
        _ctx.HaContext.Received().CallService("cover", "set_cover_position", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("cover.rollerblind_0003")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldNotifyAboutLowBatteryWhenWakingUp()
    {
        // Arrange
        _ctx.WithEntityState("sensor.vincent_phone_battery_level", "25");
        _ctx.WithEntityState("binary_sensor.vincent_phone_is_charging", "off");
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("input_boolean.sleeping")
            .FromState("on")
            .ToState("off");

        // Assert — should notify about low battery.
        _ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone");
    }

    [Fact]
    public void ShouldWakeUpOnTvTurnOn()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "on");
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("media_player.tv")
            .FromState("off")
            .ToState("on");

        // Assert — should turn off sleeping when TV turns on.
        _ctx.HaContext.Received().CallService("input_boolean", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("input_boolean.sleeping")), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldWakeUpOnBureauLightTurnOn()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.sleeping", "on");
        _ = _ctx.InitApp<SleepManager>();

        // Act
        _ctx.ChangeStateFor("light.bureau")
            .FromState("off")
            .ToState("on");

        // Assert — should turn off sleeping when bureau light turns on.
        _ctx.HaContext.Received().CallService("input_boolean", "turn_off", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.Contains("input_boolean.sleeping")), 
            Arg.Any<object>());
    }
}