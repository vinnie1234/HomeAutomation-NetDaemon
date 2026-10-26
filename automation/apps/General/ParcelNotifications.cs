using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Parcels;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Lets Vincent and Carleen know when a parcel is announced, when the status of a parcel changes and when
/// PostNL announces a letter. Uses the events of the PostNL, DHL NL, DPD and GLS integrations: they only fire
/// for real changes (and not right after a restart), so no notification is sent twice.
/// </summary>
[NetDaemonApp(Id = nameof(ParcelNotifications))]
public class ParcelNotifications : BaseApp
{
    /// <summary>The domains of the parcel integrations; their events start with the domain.</summary>
    private static readonly string[] Integrations = ["postnl", "dhl_nl", "dpd", "gls"];

    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
    private const string Channel = "Pakketten";

    private readonly AppConfig _config;

    public ParcelNotifications(
        IHaContext ha,
        ILogger<ParcelNotifications> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _config = config.Value;

        var registered = Integrations.Select(x => $"{x}_parcel_registered").ToHashSet();
        var changed = Integrations.SelectMany(x => new[] { $"{x}_parcel_status_changed", $"{x}_parcel_delivered" }).ToHashSet();
        
        HaContext.Events
            .Where(e => registered.Contains(e.EventType) || changed.Contains(e.EventType))
            .Subscribe(e =>
            {
                var parcel = e.DataElement?.Deserialize<ParcelEvent>();
                if (parcel == null) return;

                if (registered.Contains(e.EventType)) NotifyRegistered(parcel);
                else NotifyStatusChanged(parcel);
            });

        HaContext.Events
            .Where(e => e.EventType == "postnl_letter_announced")
            .Subscribe(e =>
            {
                var letter = e.DataElement?.Deserialize<LetterEvent>();
                if (letter != null) NotifyLetter(letter);
            });
    }

    private void NotifyRegistered(ParcelEvent parcel)
    {
        var message = string.Join(" · ", new[] { FromSender(parcel), DeliveryWindow(parcel) }.Where(x => x != null));
        Logger.LogInformation("Nieuw pakket van {Carrier}: {Message}", parcel.Carrier, message);
        Send($"📦 Nieuw pakket via {CarrierName(parcel)}", message, parcel);
    }

    private void NotifyStatusChanged(ParcelEvent parcel)
    {
        var status = parcel.Status switch
        {
            "registered" => "aangemeld",
            "in_transit" => "onderweg",
            "out_for_delivery" => "wordt vandaag bezorgd",
            "at_pickup_point" => "ligt klaar om af te halen",
            "delivered" => "bezorgd",
            "returning" => "gaat terug naar de afzender",
            "problem" => "probleem met de bezorging",
            _ => parcel.RawStatus ?? "nieuwe status"
        };

        var details = parcel.Status switch
        {
            "at_pickup_point" => PickupPoint(parcel) is { } point ? $"bij {point}" : null,
            "out_for_delivery" or "in_transit" => DeliveryWindow(parcel),
            "problem" or "returning" or "unknown" => parcel.RawStatus,
            _ => null
        };

        var message = string.Join(" · ", new[] { FromSender(parcel) ?? "Pakket", details }.Where(x => x != null));
        Logger.LogInformation("Pakket van {Carrier} {Status}: {Message}", parcel.Carrier, status, message);
        Send($"📦 {CarrierName(parcel)}: {status}", message, parcel);
    }

    private void NotifyLetter(LetterEvent letter)
    {
        var date = DateOnly.TryParse(letter.Date, CultureInfo.InvariantCulture, out var day) ? DayName(day) : null;
        var message = date != null ? $"PostNL bezorgt {date} een brief." : $"PostNL heeft een brief aangekondigd ({letter.Title}).";
        Logger.LogInformation("Brief aangekondigd: {Title}", letter.Title);

        Notify.NotifyPhoneVincentCarleen("✉️ Er komt post aan", message, true,
            action: [new ActionModel(action: "URI", title: "Bekijk", uri: DashboardUrl)],
            image: letter.ImageUrl, channel: Channel);
    }

    private void Send(string title, string message, ParcelEvent parcel)
    {
        List<ActionModel> actions = [new(action: "URI", title: "Bekijk", uri: DashboardUrl)];
        if (!string.IsNullOrEmpty(parcel.Url)) actions.Add(new ActionModel(action: "URI", title: "Volgen", uri: parcel.Url));

        Notify.NotifyPhoneVincentCarleen(title, message, true, action: actions, channel: Channel);
    }

    private string DashboardUrl => _config.BaseUrlHomeAssistant + "/dashboard-huis/huishouden";

    private static string CarrierName(ParcelEvent parcel) => string.IsNullOrWhiteSpace(parcel.Carrier) ? "de bezorger" : parcel.Carrier;

    private static string? FromSender(ParcelEvent parcel) => string.IsNullOrWhiteSpace(parcel.Sender) ? null : $"Van {parcel.Sender}";

    private static string? PickupPoint(ParcelEvent parcel) => parcel.PickupPoint switch
    {
        { ValueKind: JsonValueKind.String } point => point.GetString(),
        { ValueKind: JsonValueKind.Object } point when point.TryGetProperty("name", out var name) => name.GetString(),
        _ => null
    };

    /// <summary>
    /// The expected delivery in Dutch time, e.g. "vandaag 14:00–16:00", "morgen vanaf 09:00" or "do 22 okt".
    /// </summary>
    private string? DeliveryWindow(ParcelEvent parcel)
    {
        var from = ParseTime(parcel.PlannedFrom);
        var to = ParseTime(parcel.PlannedTo);
        if (from == null && to == null) return null;

        var day = DayName(DateOnly.FromDateTime((from ?? to)!.Value.DateTime));
        // Some carriers only know the day, not the time
        if (DateOnly.TryParseExact(parcel.PlannedFrom ?? parcel.PlannedTo, "yyyy-MM-dd", out var dateOnly))
            return $"verwacht {DayName(dateOnly)}";

        return (from, to) switch
        {
            ({ } start, { } end) when start != end => $"verwacht {day} {start:HH:mm}–{end:HH:mm}",
            ({ } start, _) => $"verwacht {day} vanaf {start:HH:mm}",
            (_, { } end) => $"verwacht {day} vóór {end:HH:mm}",
            _ => null
        };
    }

    private static DateTimeOffset? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? Extensions.SchedulerExtensions.ToDutchTime(time)
            : null;

    private string DayName(DateOnly day)
    {
        var today = DateOnly.FromDateTime(Scheduler.NowNl().DateTime);
        return (day.DayNumber - today.DayNumber) switch
        {
            0 => "vandaag",
            1 => "morgen",
            _ => "op " + day.ToString("ddd d MMM", Dutch)
        };
    }
}
