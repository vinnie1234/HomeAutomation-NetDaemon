using Automation.apps.General;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;
using System.Text.Json;

namespace TestAutomation.Apps.General;

public class AutoUpdateAppTests
{
    [Fact]
    public async Task Constructor_SchedulesAutoUpdate_WhenCronFires_UpdatesAndNotifies()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var config = new AppConfig
        {
            BaseUrlHomeAssistant = "http://localhost:8123",
            Discord = new DiscordConfig { Updates = "updates_channel" }
        };

        var updateEntity1 = new EntityState
        {
            EntityId = "update.test1",
            State = "on",
            AttributesJson = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new { friendly_name = "Update 1" }))
        };
        var updateEntity2 = new EntityState
        {
            EntityId = "update.test2",
            State = "off",
            AttributesJson = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new { friendly_name = "Update 2" }))
        };

        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { 
            new Entity(ctx.HaContext, "update.test1"), 
            new Entity(ctx.HaContext, "update.test2") 
        });
        ctx.HaContext.GetState("update.test1").Returns(updateEntity1);
        ctx.HaContext.GetState("update.test2").Returns(updateEntity2);

        // Act
        var app = ctx.InitApp<AutoUpdateApp>(Options.Create(config));
        
        // Fast forward 31 days to ensure we hit the 30th of the month at 11:00
        ctx.AdvanceTimeBy(TimeSpan.FromDays(35).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        // Need to check if CallService was called to install the update
        ctx.VerifyCallService("update", "install", "test1", times: 1);
        ctx.VerifyCallService("update", "install", "test2", times: 0);

        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(2).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();
        await Task.Delay(100);

        // Verify NotifyDiscord
        ctx.VerifyCallNotify("notify", "discord_homeassistant", times: 3); // Because: 1 summary, 1 start update, 1 finish update
        
        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
    }
}



