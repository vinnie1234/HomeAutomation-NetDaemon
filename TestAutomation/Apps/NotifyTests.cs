using System.Reactive.Subjects;
using System.Text.Json;
using Automation.Interfaces;
using Automation.Models;
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

    private static bool IsClearNotification(object? parameters, string tag)
    {
        var json = JsonSerializer.SerializeToElement(parameters);
        return json.GetProperty("message").GetString() == "clear_notification" &&
               json.GetProperty("data").GetProperty("tag").GetString() == tag;
    }
}
