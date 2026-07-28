using Automation.apps.Rooms.LivingRoom;
using Automation.Enum;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class TvTests
{
    private void SetupDefaultStates(AppTestContext ctx)
    {
        ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { EntityId = "input_boolean.disablelightautomationlivingroom", State = "off" });
        ctx.HaContext.GetState("input_boolean.working").Returns(new EntityState { EntityId = "input_boolean.working", State = "off" });
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Day" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
    }

    [Fact]
    public void TvTurnsOn_ActivatesMovieScene_WhenLightAutomationsEnabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("scene", "turn_on", "woonkamer_movie_2", times: 1);
    }

    [Fact]
    public void TvTurnsOn_TurnsOffMultipleLights()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("light", "turn_off", "plafond_woonkamer", times: 1);
        ctx.VerifyCallService("light", "turn_off", "hue_filament_bulb_1", times: 1);
        ctx.VerifyCallService("light", "turn_off", "hue_filament_bulb_2", times: 1);
        ctx.VerifyCallService("light", "turn_off", "lampen_keuken", times: 1);
        ctx.VerifyCallService("light", "turn_off", "plafond", times: 1);
        ctx.VerifyCallService("light", "turn_off", "nachtkastje", times: 1);
    }

    [Fact]
    public void TvTurnsOn_TurnsSoundbarOn()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("media_player", "turn_on", "av_soundbar", times: 1);
    }

    [Fact]
    public void TvTurnsOn_SetsVolume()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("media_player", "volume_set", "tv", times: 1);
    }

    [Fact]
    public void TvTurnsOn_DoesNothing_WhenLightAutomationsDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { EntityId = "input_boolean.disablelightautomationlivingroom", State = "on" });
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyNotCallService("scene.turn_on");
        ctx.VerifyNotCallService("light.turn_off");
        ctx.VerifyNotCallService("media_player.turn_on");
    }

    [Fact]
    public void TvTurnsOff_ActivatesDayScene_WhenDayMode()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Day" });
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("scene", "turn_on", "woonkamerday", times: 1);
    }

    [Fact]
    public void TvTurnsOff_ActivatesEveningScene_WhenEveningMode()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Evening" });
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("scene", "turn_on", "woonkamerevening", times: 1);
    }

    [Fact]
    public void TvTurnsOff_ActivatesMorningScene_WhenMorningMode()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Morning" });
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("scene", "turn_on", "woonkamermorning", times: 1);
    }

    [Fact]
    public void TvTurnsOff_ActivatesNightScene_WhenNightMode()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Night" });
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("scene", "turn_on", "woonkamernight", times: 1);
    }

    [Fact]
    public void TvTurnsOff_TurnsSoundbarOff()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("media_player", "turn_off", "av_soundbar", times: 1);
    }

    [Fact]
    public void TvTurnsOff_TurnsPs5Off()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallService("switch", "turn_off", "ps5_vincent_power", times: 1);
    }

    [Fact]
    public void TvTurnsOff_TurnsPlafondOn_WhenWorking()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.WithEntityState("input_boolean.working", "on");
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyCallServiceWithData<HomeAssistantGenerated.LightTurnOnParameters>("light", "turn_on", "plafond", new HomeAssistantGenerated.LightTurnOnParameters(), times: 1);
    }

    [Fact]
    public void TvTurnsOff_DoesNothing_WhenLightAutomationsDisabled()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablelightautomationlivingroom").Returns(new EntityState { EntityId = "input_boolean.disablelightautomationlivingroom", State = "on" });
        var app = ctx.InitApp<Tv>();

        // Act
        ctx.ChangeStateFor("media_player.tv").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();
        System.Threading.Thread.Sleep(50);

        // Assert
        ctx.VerifyNotCallService("scene.turn_on");
        ctx.VerifyNotCallService("media_player.turn_off");
        ctx.VerifyNotCallService("switch.turn_off");
    }
}

