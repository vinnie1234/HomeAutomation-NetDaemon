using System;
using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class TodoManagerTests
{
    private static IOptions<AppConfig> CreateConfig()
    {
        return Options.Create(new AppConfig
        {
            Discord = new DiscordConfig { TODO = "discord_todo_channel" },
            BaseUrlHomeAssistant = "http://localhost:8123"
        });
    }

    [Fact]
    public void TodoManager_ShouldInitializeWithoutErrors()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();

        // Act
        var app = ctx.InitApp<TodoManager>(CreateConfig());

        // Assert
        Assert.NotNull(app);
    }

    [Fact]
    public void HandleTodoList_ShouldReturn_WhenDisableResetIsOn()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "on" });
        var startTime = new DateTime(2023, 10, 10, 21, 0, 0);
        ctx.SetCurrentTime(startTime);
        
        ctx.InitApp<TodoManager>(CreateConfig());

        // Act - advance time to trigger cron "00 22 * * *"
        ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(1).Ticks);

        // Assert
        ctx.VerifyNotCallService("todo.get_items");
        ctx.VerifyNotCallService("todo.add_item");
    }

    [Fact]
    public void HandleTodoList_ShouldAddTodoItems_WhenDisableResetIsOff()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        
        // Mock the response of GetItems service call. 
        // We might not be able to easily mock the exact Task result of the extension method, 
        // but we can at least assert that the add_item was called at the end.
        ctx.HaContext.CallServiceWithResponseAsync("todo", "get_items", Arg.Any<ServiceTarget>(), Arg.Any<object>())
            .Returns(JsonDocument.Parse("{\"todo.dagelijks\": {\"items\": []}}").RootElement);

        ctx.InitApp<TodoManager>(CreateConfig());

        // Act - advance time to 01:00 the next day
        ctx.AdvanceTimeBy(TimeSpan.FromHours(25).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("todo", "add_item", "dagelijks", times: 2);
    }
}


