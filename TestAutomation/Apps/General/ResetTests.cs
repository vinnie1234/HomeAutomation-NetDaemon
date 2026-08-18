using System.Text.Json;
using Automation.apps.General;
using Automation.Interfaces;
using Automation.Models;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class ResetTests
{
    [Fact]
    public void Reset_ShouldNotReset_WhenDisableResetIsOn()
    {
        // Arrange
        using var ctx = AppTestContext.New();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "on" });

        // Act
        var app = ctx.InitApp<Reset>(storage);

        // Assert
        storage.DidNotReceive().Get<List<LightStateModel>>("LightState");
    }

    [Fact]
    public void Reset_ShouldResetLightsOff_WhenIsOnIsFalse()
    {
        // Arrange
        using var ctx = AppTestContext.New();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        
        var lightStates = new List<LightStateModel>
        {
            new("light.bureau", null, null, null, false, null)
        };
        storage.Get<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        var app = ctx.InitApp<Reset>(storage);

        // Assert
        ctx.VerifyCallService("light", "turn_off", "bureau", times: 1);
    }

    [Fact]
    public void Reset_ShouldResetLightsWithRgb_WhenSupportedColorModesContainsXy()
    {
        // Arrange
        using var ctx = AppTestContext.New();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        
        // Mock light state in HomeAssistant context so it has the supported color modes in attributes
        var attributes = new Dictionary<string, object>
        {
            { "supported_color_modes", new[] { "xy", "color_temp" } }
        };
        ctx.HaContext.GetState("light.bureau").Returns(new EntityState
        {
            EntityId = "light.bureau",
            State = "on",
            AttributesJson = JsonSerializer.SerializeToElement(attributes)
        });
        
        var lightStates = new List<LightStateModel>
        {
            new("light.bureau", new List<double> { 255, 128, 64 }, 100, null, true, new List<string> { "xy" })
        };
        storage.Get<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        var app = ctx.InitApp<Reset>(storage);

        // Assert
        ctx.VerifyCallService("light", "turn_on", "bureau", times: 1);
    }

    [Fact]
    public void Reset_ShouldResetLightsWithColorTemp_WhenSupportedColorModesDoesNotContainXyAndContainsOtherModes()
    {
        // Arrange
        using var ctx = AppTestContext.New();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        
        var attributes = new Dictionary<string, object>
        {
            { "supported_color_modes", new[] { "color_temp" } }
        };
        ctx.HaContext.GetState("light.bureau").Returns(new EntityState
        {
            EntityId = "light.bureau",
            State = "on",
            AttributesJson = JsonSerializer.SerializeToElement(attributes)
        });
        
        var lightStates = new List<LightStateModel>
        {
            new("light.bureau", null, 100, 3000, true, new List<string> { "color_temp" })
        };
        storage.Get<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        var app = ctx.InitApp<Reset>(storage);

        // Assert
        ctx.VerifyCallService("light", "turn_on", "bureau", times: 1);
    }

    [Fact]
    public void Reset_ShouldResetLightsOnOff_WhenSupportedColorModesContainsOnOff()
    {
        // Arrange
        using var ctx = AppTestContext.New();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        
        var attributes = new Dictionary<string, object>
        {
            { "supported_color_modes", new[] { "onoff" } }
        };
        ctx.HaContext.GetState("light.bureau").Returns(new EntityState
        {
            EntityId = "light.bureau",
            State = "on",
            AttributesJson = JsonSerializer.SerializeToElement(attributes)
        });
        
        var lightStates = new List<LightStateModel>
        {
            new("light.bureau", null, null, null, true, new List<string> { "onoff" })
        };
        storage.Get<List<LightStateModel>>("LightState").Returns(lightStates);

        // Act
        var app = ctx.InitApp<Reset>(storage);

        // Assert
        ctx.VerifyCallService("light", "turn_on", "bureau", times: 1);
    }
}

