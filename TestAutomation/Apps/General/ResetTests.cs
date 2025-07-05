using Automation.apps.General;
using Automation.Interfaces;
using Automation.Models;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using System.Text.Json;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class ResetTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();

    [Fact]
    public void ShouldSkipResetWhenDisabled()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablereset", "on");

        // Act
        _ = _ctx.InitApp<Reset>(_storage);

        // Assert — should not call storage when reset is disabled.
        TestDebugHelper.AssertCallWithDebug(_storage, storage =>
        {
            storage.DidNotReceive().GetAsync<List<LightStateModel>>("LightState");
        }, nameof(ShouldSkipResetWhenDisabled));
    }

    [Fact]
    public void ShouldRestoreLightStateWhenEnabled()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablereset", "off");
        var lightStates = new List<LightStateModel>
        {
            new(entityId: "light.bureau", rgbColors: new List<double> { 255, 128, 64 }, 
                brightness: 200, colorTemp: null, isOn: true, supportedColorModes: new[] { "xy" })
        };
        _storage.GetAsync<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        _ = _ctx.InitApp<Reset>(_storage);

        // Assert — should restore light states from storage.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldRestoreLightStateWhenEnabled));
    }

    [Fact]
    public void ShouldTurnOffLightWhenPreviousStateWasOff()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablereset", "off");
        var lightStates = new List<LightStateModel>
        {
            new(entityId: "light.bureau", rgbColors: null, 
                brightness: 0, colorTemp: null, isOn: false, supportedColorModes: new[] { "onoff" })
        };
        _storage.GetAsync<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        _ = _ctx.InitApp<Reset>(_storage);

        // Assert — should turn off light when previous state was off.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_off", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffLightWhenPreviousStateWasOff));
    }

    [Fact]
    public void ShouldRestoreColorTempForCompatibleLights()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablereset", "off");
        var lightStates = new List<LightStateModel>
        {
            new(entityId: "light.bureau", rgbColors: null, 
                brightness: 150, colorTemp: JsonSerializer.SerializeToElement(4000), isOn: true, supportedColorModes: new[] { "color_temp" })
        };
        _storage.GetAsync<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        _ = _ctx.InitApp<Reset>(_storage);

        // Assert — should restore color temperature for compatible lights.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("light", "turn_on", 
                Arg.Any<ServiceTarget>(), 
                Arg.Any<object>());
        }, nameof(ShouldRestoreColorTempForCompatibleLights));
    }

    [Fact]
    public void ShouldNotifyAboutDeletedAlarms()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.disablereset", "off");
        var oldAlarms = new List<AlarmStateModel?>
        {
            new() { AlarmId = "alarm1", Status = "set", LocalTime = "08:00", EntityId = "hub.vincent_alarms" }
        };
        _storage.GetAsync<List<AlarmStateModel?>>("LightState").Returns(oldAlarms);

        // Act
        _ = _ctx.InitApp<Reset>(_storage);

        // Assert — should check for alarm differences.
        TestDebugHelper.AssertCallWithDebug(_storage, storage =>
        {
            storage.Received().GetAsync<List<AlarmStateModel?>>("LightState");
        }, nameof(ShouldNotifyAboutDeletedAlarms));
    }
}