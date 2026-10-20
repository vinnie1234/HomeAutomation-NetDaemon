using System.Reactive.Subjects;
using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using FluentAssertions;
using HomeAssistantGenerated;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class ParcelNotificationsTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly Subject<Event> _events = new();

    public ParcelNotificationsTests()
    {
        _ctx.HaContext.Events.Returns(_events);
        // Tuesday 20 October 2026, 10:00 Dutch time (summer time, UTC+2)
        _ctx.SetCurrentTime(new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc));
        _ctx.InitApp<ParcelNotifications>(Options.Create(new AppConfig { BaseUrlHomeAssistant = "http://ha" }));
    }

    private void SendEvent(string eventType, object data) =>
        _events.OnNext(new Event { EventType = eventType, DataElement = JsonSerializer.SerializeToElement(data) });

    private List<NotifyMobileAppVincentPhoneParameters> VincentNotifications() =>
        _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "mobile_app_vincent_phone")
            .Select(c => (NotifyMobileAppVincentPhoneParameters)c.GetArguments()[3]!)
            .ToList();

    [Fact]
    public void NewParcel_NotifiesBothPhonesWithCarrierSenderAndDeliveryWindow()
    {
        SendEvent("dhl_nl_parcel_registered", new
        {
            carrier = "DHL", barcode = "JVGL123", sender = "Coolblue", status = "registered",
            planned_from = "2026-10-21T12:00:00+00:00", planned_to = "2026-10-21T14:00:00+00:00",
            url = "https://dhl.nl/track/JVGL123"
        });

        var notification = VincentNotifications().Should().ContainSingle().Subject;
        notification.Title.Should().Be("📦 Nieuw pakket via DHL");
        notification.Message.Should().Be("Van Coolblue · verwacht morgen 14:00–16:00");
        _ctx.VerifyCallNotify("notify", "mobile_app_carleen_mobiel");
    }

    [Fact]
    public void StatusChange_ToPickupPoint_TellsWhereToPickItUp()
    {
        SendEvent("gls_parcel_status_changed", new
        {
            carrier = "GLS", sender = "Zalando", status = "at_pickup_point", pickup_point = "Primera Centrum",
            old_status = "in_transit", new_status = "at_pickup_point"
        });

        var notification = VincentNotifications().Should().ContainSingle().Subject;
        notification.Title.Should().Be("📦 GLS: ligt klaar om af te halen");
        notification.Message.Should().Be("Van Zalando · bij Primera Centrum");
    }

    [Fact]
    public void StatusChange_PickupPointAsObject_UsesItsName()
    {
        SendEvent("dpd_parcel_status_changed", new
        {
            carrier = "DPD", status = "at_pickup_point", pickup_point = new { name = "DPD Pickup Albert Heijn" }
        });

        VincentNotifications().Should().ContainSingle().Which.Message.Should().Be("Pakket · bij DPD Pickup Albert Heijn");
    }

    [Fact]
    public void Delivered_SendsDeliveredNotification()
    {
        SendEvent("postnl_parcel_delivered", new { carrier = "PostNL", sender = "Bol.com", status = "delivered" });

        var notification = VincentNotifications().Should().ContainSingle().Subject;
        notification.Title.Should().Be("📦 PostNL: bezorgd");
        notification.Message.Should().Be("Van Bol.com");
    }

    [Fact]
    public void OutForDelivery_WithOnlyADay_ShowsTheDay()
    {
        SendEvent("postnl_parcel_status_changed", new
        {
            carrier = "PostNL", sender = "Bol.com", status = "out_for_delivery", planned_from = "2026-10-20"
        });

        VincentNotifications().Should().ContainSingle().Which.Message.Should().Be("Van Bol.com · verwacht vandaag");
    }

    [Fact]
    public void NewLetter_NotifiesWithTheScan()
    {
        SendEvent("postnl_letter_announced", new
        {
            id = "abc", title = "Brief 22 oktober", date = "2026-10-22", image_url = "https://postnl.nl/scan.jpg", carrier = "PostNL"
        });

        var notification = VincentNotifications().Should().ContainSingle().Subject;
        notification.Title.Should().Be("✉️ Er komt post aan");
        notification.Message.Should().Be("PostNL bezorgt op do 22 okt een brief.");
        JsonSerializer.Serialize(notification.Data).Should().Contain("https://postnl.nl/scan.jpg");
    }

    [Fact]
    public void OtherEvents_AreIgnored()
    {
        SendEvent("postnl_parcel_delivery_time_changed", new { carrier = "PostNL", status = "in_transit" });
        SendEvent("postnl_outgoing_parcel_delivered", new { carrier = "PostNL", status = "delivered" });

        VincentNotifications().Should().BeEmpty();
    }
}
