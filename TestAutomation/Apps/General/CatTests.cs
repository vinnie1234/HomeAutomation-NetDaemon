﻿using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using HomeAssistantGenerated;

namespace TestAutomation.Apps.General;

public class CatTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldFeedCatWhenFeedButtonIsPressed()
    {
        // Arrange
        _ctx.WithEntityState("input_number.pixelnumberofmanualfood", 5.0)
            .WithEntityState("input_number.pixellastamountmanualfeed", 0.0)
            .WithEntityState("input_number.pixeltotalamountfeedday", 10.0)
            .WithEntityState("input_number.pixeltotalamountfeedalltime", 100.0);

        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("input_button.feedcat")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("localtuya", "set_dp", 
                null, 
                Arg.Is<LocaltuyaSetDpParameters>(p => p.Dp != null && (int)p.Dp == 3 && p.Value != null && p.Value.Equals(5)));
        }, nameof(ShouldFeedCatWhenFeedButtonIsPressed));

        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_number", "set_value",
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_number.pixeltotalamountfeedday"),
                Arg.Is<InputNumberSetValueParameters>(p => p.Value != null && Math.Abs((double)(p.Value - 15.0)) < 0.01));
        }, nameof(ShouldFeedCatWhenFeedButtonIsPressed));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_number", "set_value", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_number.pixeltotalamountfeedalltime"), 
                Arg.Is<InputNumberSetValueParameters>(p => p.Value != null && Math.Abs((double)(p.Value - 105.0)) < 0.01));
        }, nameof(ShouldFeedCatWhenFeedButtonIsPressed));
    }

    [Fact]
    public void ShouldUpdateFeedCountersWhenManualFeedingOccurs()
    {
        // Arrange
        _ctx.WithEntityState("input_number.pixelnumberofmanualfood", 3.0)
            .WithEntityState("input_number.pixellastamountmanualfeed", 2.0);

        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("input_button.feedcat")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_number", "set_value", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_number.pixellastamountmanualfeed"), 
                Arg.Is<InputNumberSetValueParameters>(p => p.Value != null && Math.Abs((double)(p.Value - 5.0)) < 0.01)); // 3 + 2
        }, nameof(ShouldUpdateFeedCountersWhenManualFeedingOccurs));
    }

    [Fact]
    public void ShouldUpdateLastManualFeedDateTimeWhenFeeding()
    {
        // Arrange
        _ctx.WithEntityState("input_number.pixelnumberofmanualfood", 2.0)
            .WithEntityState("input_number.pixellastamountmanualfeed", 0.0);

        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("input_button.feedcat")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_datetime", "set_datetime", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_datetime.pixellastmanualfeed"), 
                Arg.Any<InputDatetimeSetDatetimeParameters>());
        }, nameof(ShouldUpdateLastManualFeedDateTimeWhenFeeding));
    }

    [Fact]
    public void ShouldIncrementPixelInitCounterWhenCatEntersLitterBox()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("pet_into");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("counter", "increment", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "counter.petsnowylitterboxpixelinit"), 
                Arg.Any<object>());
        }, nameof(ShouldIncrementPixelInitCounterWhenCatEntersLitterBox));
    }

    [Fact]
    public void ShouldIncrementCleaningCounterWhenLitterBoxIsCleaning()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("cleaning");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("counter", "increment", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "counter.petsnowylittleboxcleaning"), 
                Arg.Any<object>());
        }, nameof(ShouldIncrementCleaningCounterWhenLitterBoxIsCleaning));
    }

    [Fact]
    public void ShouldIncrementEmptyingCounterWhenLitterBoxIsEmptying()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("emptying");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("counter", "increment", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "counter.petsnowylitterboxemptying"), 
                Arg.Any<object>());
        }, nameof(ShouldIncrementEmptyingCounterWhenLitterBoxIsEmptying));
    }

    [Fact]
    public void ShouldNotIncrementCounterForUnknownLitterBoxStatus()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("sensor.petsnowy_litterbox_status")
            .FromState("idle")
            .ToState("unknown_state");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("counter", "increment", 
                Arg.Any<ServiceTarget>(), Arg.Any<object>());
        }, nameof(ShouldNotIncrementCounterForUnknownLitterBoxStatus));
    }

    [Fact]
    public void ShouldGiveNextFeedEarlyWhenButtonIsPressed()
    {
        // Arrange
        _ctx.WithEntityState("input_datetime.pixelfeedfirsttime", "08:00:00")
            .WithEntityState("input_number.pixelfeedfirstamount", 5.0)
            .WithEntityState("input_datetime.pixelfeedsecondtime", "18:00:00")
            .WithEntityState("input_number.pixelfeedsecondamount", 7.0);

        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("input_button.pixelgivenextfeedeary")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_boolean", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds != null && t.EntityIds.First() == "input_boolean.pixelskipnextautofeed"), 
                Arg.Any<object>());
        }, nameof(ShouldGiveNextFeedEarlyWhenButtonIsPressed));
            
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("localtuya", "set_dp", 
                null, 
                Arg.Is<LocaltuyaSetDpParameters>(p => p.Dp != null && (int)p.Dp == 3));
        }, nameof(ShouldGiveNextFeedEarlyWhenButtonIsPressed));
    }

    [Fact]
    public void ShouldCleanPetSnowyWhenCleanButtonIsPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("input_button.cleanpetsnowy")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("localtuya", "set_dp", 
                null, 
                Arg.Is<LocaltuyaSetDpParameters>(p => p.Dp != null && (int)p.Dp == 9 && p.Value != null && p.Value.Equals("true")));
        }, nameof(ShouldCleanPetSnowyWhenCleanButtonIsPressed));
    }

    [Fact]
    public void ShouldEmptyPetSnowyWhenEmptyButtonIsPressed()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("input_button.emptypetsnowy")
            .FromState("2024-01-01 12:00:00")
            .ToState("2024-01-01 12:00:01");

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("localtuya", "set_dp", 
                null, 
                Arg.Is<LocaltuyaSetDpParameters>(p => p.Dp != null && (int)p.Dp == 109 && p.Value != null && p.Value.Equals("true")));
        }, nameof(ShouldEmptyPetSnowyWhenEmptyButtonIsPressed));
    }

    [Fact]
    public void ShouldSendDiscordNotificationWhenFountainTurnsOff()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("switch.petsnowy_fountain_ison")
            .FromState("on")
            .ToState("off");

        // Simulate the delay passing (600 seconds)
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(600).Ticks);

        // Assert — notification should be sent after delay
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("notify", "discord_homeassistant", 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldSendDiscordNotificationWhenFountainTurnsOff));
    }

    [Fact]
    public void ShouldSendDiscordNotificationWhenLitterBoxAutoCleanTurnsOff()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("switch.petsnowy_litterbox_auto_clean")
            .FromState("on")
            .ToState("off");

        // Simulate the delay passing (600 seconds)
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(600).Ticks);

        // Assert — notification should be sent after delay
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("notify", "discord_homeassistant", 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldSendDiscordNotificationWhenLitterBoxAutoCleanTurnsOff));
    }

    [Fact]
    public void ShouldNotSendNotificationIfFountainTurnsOnBeforeDelay()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act
        _ctx.ChangeStateFor("switch.petsnowy_fountain_ison")
            .FromState("on")
            .ToState("off");

        // Simulate partial delay
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(300).Ticks);

        // Turn fountain back on before delay completes
        _ctx.ChangeStateFor("switch.petsnowy_fountain_ison")
            .FromState("off")
            .ToState("on");

        // Complete the delay
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromSeconds(600).Ticks);

        // Assert — notification should not be sent
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("notify", "discord", 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldNotSendNotificationIfFountainTurnsOnBeforeDelay));
    }

    [Fact]
    public void ShouldResetDailyFeedCounterAtMidnight()
    {
        // Arrange
        _ = _ctx.InitApp<Cat>();

        // Act - simulate scheduled task firing at midnight
        _ctx.Scheduler.AdvanceTo(DateTime.Today.AddDays(1).Ticks);

        // Assert
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_number", "set_value", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_number.pixeltotalamountfeedday"), 
                Arg.Is<InputNumberSetValueParameters>(p => p.Value != null && Math.Abs((double)(p.Value - 0.0)) < 0.01));
        }, nameof(ShouldResetDailyFeedCounterAtMidnight));
    }

    [Fact]
    public void ShouldSkipAutomaticFeedingWhenSkipFlagIsOn()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.pixelskipnextautofeed", "on")
            .WithEntityState("input_datetime.pixelfeedfirsttime", "08:00:00")
            .WithEntityState("input_number.pixelfeedfirstamount", 5.0);

        _ = _ctx.InitApp<Cat>();

        // Act - simulate scheduled feeding time
        _ctx.Scheduler.AdvanceTo(DateTime.Today.AddHours(8).Ticks);

        // Assert — feeding should not occur
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("localtuya", "set_dp", 
                null, 
                Arg.Is<LocaltuyaSetDpParameters>(p => p.Dp != null && (int)p.Dp == 3));
        }, nameof(ShouldSkipAutomaticFeedingWhenSkipFlagIsOn));
    }

    [Fact]
    public void ShouldTurnOffSkipFlagAfterScheduledFeedingTime()
    {
        // Arrange
        _ctx.WithEntityState("input_boolean.pixelskipnextautofeed", "on")
            .WithEntityState("input_datetime.pixelfeedfirsttime", "08:00:00")
            .WithEntityState("input_number.pixelfeedfirstamount", 5.0);

        _ = _ctx.InitApp<Cat>();

        // Act - simulate scheduled feeding time
        _ctx.Scheduler.AdvanceTo(DateTime.Today.AddHours(8).Ticks);

        // Assert — skip flag should be turned off
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received(1).CallService("input_boolean", "turn_off", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_boolean.pixelskipnextautofeed"), 
                Arg.Any<object>());
        }, nameof(ShouldTurnOffSkipFlagAfterScheduledFeedingTime));
    }
}