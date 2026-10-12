using System.Reactive.Subjects;
using System.Text.Json;
using Automation.Interfaces;
using Automation.Models;
using Automation.Repository;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using Xunit;
using Notify = Automation.apps.Notify;

namespace TestAutomation.Apps;

public class NotifyTests
{
    [Fact]
    public void ActionPressed_InvokesActionAndClearsNotificationOnAllPhones()
    {
        // Arrange
        var ha = Substitute.For<IHaContext>();
        var events = new Subject<Event>();
        ha.Events.Returns(events);
        var notify = new Notify(ha, Substitute.For<IDataRepository>(), Substitute.For<ILogger<Notify>>());

        var pressed = false;
        var action = new ActionModel("HANG_WAS_BUITEN", "✅ Was buiten gehangen", func: () => pressed = true);
        notify.NotifyPhoneVincent("Was", "De was is klaar", true, action: [action]);

        var sentParameters = ha.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IHaContext.CallService))
            .GetArguments()[3];
        var tag = JsonSerializer.SerializeToElement(sentParameters).GetProperty("data").GetProperty("tag").GetString()!;
        Assert.StartsWith("actionable-", tag);
        ha.ClearReceivedCalls();

        // Act
        events.OnNext(new Event
        {
            EventType = "mobile_app_notification_action",
            DataElement = JsonSerializer.SerializeToElement(new { action = action.Action })
        });

        // Assert
        Assert.True(pressed);
        foreach (var phone in new[] { "mobile_app_vincent_phone", "mobile_app_carleen_mobiel" })
        {
            ha.Received(1).CallService("notify", phone, null,
                Arg.Is<object?>(d => IsClearNotification(d, tag)));
        }
    }

    [Theory]
    [InlineData("on", false)]
    [InlineData("off", true)]
    public void NotifyHouse_OnlyAnnouncesWhenSomeoneIsHome(string away, bool expectAnnouncement)
    {
        // Arrange
        var ha = Substitute.For<IHaContext>();
        ha.GetState("input_boolean.away").Returns(new EntityState { EntityId = "input_boolean.away", State = away });
        var notify = new Notify(ha, Substitute.For<IDataRepository>(), Substitute.For<ILogger<Notify>>());

        // Act
        notify.NotifyHouse("Test", "Hallo", true);

        // Assert
        var announced = ha.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(IHaContext.CallService) && (string)c.GetArguments()[0]! == "tts");
        Assert.Equal(expectAnnouncement, announced);
    }

    [Theory]
    [InlineData("input_boolean.sleepingvincent")]
    [InlineData("input_boolean.sleepingcarleen")]
    public void Speakers_AreNeverUsed_WhileSomeoneIsSleeping(string sleepingEntityId)
    {
        // Arrange - someone is home and awake enough to trigger something, but one of us is sleeping
        var ha = Substitute.For<IHaContext>();
        ha.GetState("input_boolean.away").Returns(new EntityState { EntityId = "input_boolean.away", State = "off" });
        ha.GetState(sleepingEntityId).Returns(new EntityState { EntityId = sleepingEntityId, State = "on" });
        var notify = new Notify(ha, Substitute.For<IDataRepository>(), Substitute.For<ILogger<Notify>>());

        // Act
        notify.NotifyHouse("Test", "Hallo", true);
        notify.SendMusicToHome("http://example.test/music.mp3");

        // Assert - no announcement, no music, not even a volume change
        Assert.DoesNotContain(ha.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IHaContext.CallService));
    }

    [Fact]
    public void ThrottledNotification_IsNotSentAgain_WithinTheWaitingTime()
    {
        // Arrange - real repository, so the stored "last sent" time is actually read back
        var ha = Substitute.For<IHaContext>();
        var storage = new DataRepository(Path.Combine(Path.GetTempPath(), $"notify-test-{Guid.NewGuid()}"), Substitute.For<ILogger>());
        var notify = new Notify(ha, storage, Substitute.For<ILogger<Notify>>());

        // Act
        notify.NotifyPhoneVincent("Batterij", "bijna leeg", false, sendAfterMinutes: 60);
        notify.NotifyPhoneVincent("Batterij", "bijna leeg", false, sendAfterMinutes: 60);

        // Assert - only the first one is sent
        Assert.Single(ha.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IHaContext.CallService));
    }

    [Fact]
    public void ThrottledNotification_IsSentAgain_AfterTheWaitingTime()
    {
        // Arrange - the last notification with this title was sent 61 minutes ago
        var ha = Substitute.For<IHaContext>();
        var storage = new DataRepository(Path.Combine(Path.GetTempPath(), $"notify-test-{Guid.NewGuid()}"), Substitute.For<ILogger>());
        storage.Save("notificationHistory", new List<NotificationModel>
        {
            new("Batterij", "bijna leeg", DateTimeOffset.Now.AddMinutes(-61))
        });
        var notify = new Notify(ha, storage, Substitute.For<ILogger<Notify>>());

        // Act
        notify.NotifyPhoneVincent("Batterij", "bijna leeg", false, sendAfterMinutes: 60);
        notify.NotifyPhoneVincent("Batterij", "bijna leeg", false, sendAfterMinutes: 60);

        // Assert - sent once, and the stored time was updated so the second one is held back
        Assert.Single(ha.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IHaContext.CallService));
    }

    [Fact]
    public void ActionPressedTwice_RunsTheActionOnlyOnce()
    {
        // Arrange - e.g. Vincent and Carleen both press the button of the same notification
        var ha = Substitute.For<IHaContext>();
        var events = new Subject<Event>();
        ha.Events.Returns(events);
        var notify = new Notify(ha, Substitute.For<IDataRepository>(), Substitute.For<ILogger<Notify>>());

        var pressedCount = 0;
        var action = new ActionModel("WAS_BINNEN_GEHAALD", "✅ Was binnen gehaald", func: () => pressedCount++);
        notify.NotifyPhoneVincentCarleen("Was", "Haal de was binnen", true, action: [action]);
        var pressEvent = new Event
        {
            EventType = "mobile_app_notification_action",
            DataElement = JsonSerializer.SerializeToElement(new { action = action.Action })
        };

        // Act
        events.OnNext(pressEvent);
        events.OnNext(pressEvent);

        // Assert
        Assert.Equal(1, pressedCount);
    }

    [Fact]
    public void TtsNotification_UsesTheProcessChannel()
    {
        // Arrange
        var ha = Substitute.For<IHaContext>();
        var notify = new Notify(ha, Substitute.For<IDataRepository>(), Substitute.For<ILogger<Notify>>());

        // Act
        notify.NotifyPhoneVincentTts("Test", "Hallo", true);

        // Assert
        var parameters = ha.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IHaContext.CallService)).GetArguments()[3];
        var data = JsonSerializer.SerializeToElement(parameters).GetProperty("data");
        Assert.Equal("process", data.GetProperty("channel").GetString());
        Assert.Equal("Hallo", data.GetProperty("tts_text").GetString());
    }

    private static bool IsClearNotification(object? parameters, string tag)
    {
        var json = JsonSerializer.SerializeToElement(parameters);
        return json.GetProperty("message").GetString() == "clear_notification" &&
               json.GetProperty("data").GetProperty("tag").GetString() == tag;
    }
}
