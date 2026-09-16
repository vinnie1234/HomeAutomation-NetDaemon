using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

/// <summary>
/// Unit tests for the <see cref="Laundry"/> automation.
///
/// Because the forecast is fetched via <c>CallServiceWithResponseAsync</c> (async HA service call)
/// the tests mock the <see cref="IHaContext"/> to return a pre-built <see cref="JsonElement"/> for
/// the <c>weather.get_forecasts</c> call, then verify the resulting notification behaviour.
/// </summary>
public class LaundryTests
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Helpers / shared state
    // ──────────────────────────────────────────────────────────────────────────

    private readonly AppTestContext _ctx;
    private readonly INotify _notify;
    private readonly ILogger<Laundry> _logger;
    private readonly IOptions<AppConfig> _config;

    public LaundryTests()
    {
        _ctx = AppTestContext.NewWithScheduler();
        _notify = Substitute.For<INotify>();
        _logger = Substitute.For<ILogger<Laundry>>();
        _config = Options.Create(new AppConfig
        {
            BaseUrlHomeAssistant = "http://homeassistant.test",
            Discord = new DiscordConfig { Logs = "logs" }
        });

        // Default: was hangt NIET buiten
        SetWasHangtBuiten(false);

        // Default: wasmachine is running
        _ctx.HaContext.GetState("sensor.wasmachine_washer_machine_state")
            .Returns(new EntityState
            {
                EntityId = "sensor.wasmachine_washer_machine_state",
                State = "run"
            });

        // Default: geen neerslag
        _ctx.HaContext.GetState("binary_sensor.weer_thuis_neerslag_verwacht")
            .Returns(new EntityState
            {
                EntityId = "binary_sensor.weer_thuis_neerslag_verwacht",
                State = "off"
            });

        // Default: zon gaat onder om 20:00 lokaal (fictief)
        SetSunNextSetting(DateTime.Today.AddHours(20).ToUniversalTime());
    }

    private Laundry CreateApp() =>
        new(_ctx.HaContext, _logger, _notify, _ctx.Scheduler, _config);

    // ──────────────────────────────────────────────────────────────────────────
    //  Test helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void SetWasHangtBuiten(bool aan)
    {
        _ctx.HaContext.GetState("input_boolean.laundryhangingoutside")
            .Returns(new EntityState
            {
                EntityId = "input_boolean.laundryhangingoutside",
                State = aan ? "on" : "off"
            });
    }

    private void SetSunNextSetting(DateTime utcTime)
    {
        _ctx.HaContext.GetState("sensor.sun_next_setting")
            .Returns(new EntityState
            {
                EntityId = "sensor.sun_next_setting",
                State = utcTime.ToString("O")
            });
    }

    /// <summary>
    /// Builds a <see cref="JsonElement"/> that mimics the HA <c>get_forecasts</c> response,
    /// and wires it up so the mocked <see cref="IHaContext"/> returns it.
    /// </summary>
    private void SetupForecast(List<(DateTime dt, double precipitation)> entries)
    {
        var forecastItems = entries.Select(e => new
        {
            datetime = e.dt.ToUniversalTime().ToString("O"),
            condition = e.precipitation > 0 ? "rainy" : "sunny",
            precipitation = e.precipitation,
            precipitation_probability = e.precipitation > 0 ? 80 : 0,
            temperature = 18
        }).ToList();

        var responseObj = new Dictionary<string, object>
        {
            ["weather.weer_thuis"] = new { forecast = forecastItems }
        };

        var json = JsonSerializer.Serialize(responseObj);
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        _ctx.HaContext
            .CallServiceWithResponseAsync(
                "weather", "get_forecasts",
                Arg.Any<ServiceTarget?>(),
                Arg.Any<object?>())
            .Returns(Task.FromResult<JsonElement?>(element));
    }

    /// <summary>
    /// Creates a list of hours starting at <paramref name="start"/> that are all dry,
    /// followed optionally by wet hours.
    /// </summary>
    private static List<(DateTime dt, double precipitation)> BuildForecast(
        DateTime start, int dryHours, int wetHoursAfter = 0, int dryHoursAfter = 0)
    {
        var list = new List<(DateTime, double)>();
        for (var i = 0; i < dryHours; i++)
            list.Add((start.AddHours(i), 0));
        for (var i = 0; i < wetHoursAfter; i++)
            list.Add((start.AddHours(dryHours + i), 0.5));
        for (var i = 0; i < dryHoursAfter; i++)
            list.Add((start.AddHours(dryHours + wetHoursAfter + i), 0));
        return list;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Tests: wasmachine klaar → notificatie
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WasmachineKlaar_Droog3Uur_StuurtPositieveNotificatie()
    {
        // Arrange: 6 dry hours starting now
        var nu = DateTime.Today.AddHours(12); // Use a fixed time
        _ctx.Scheduler.AdvanceTo(nu.Ticks);
        SetupForecast(BuildForecast(nu, dryHours: 6));
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.wasmachine_washer_machine_state")
            .FromState("run")
            .ToState("stop");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Allow async forecast call to complete
        await Task.Delay(200);

        // Assert: positive notification sent
        _notify.Received().NotifyPeopleHome(
            Arg.Is<string>(t => t.Contains("kan buiten")),
            Arg.Any<string>(),
            true,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Is<List<ActionModel>?>(a => a != null && a.Any(x => x.Title.Contains("buiten"))),
            image: Arg.Any<string?>(),
            channel: Arg.Any<string?>(),
            vibrationPattern: Arg.Any<string?>());
    }

    [Fact]
    public async Task WasmachineKlaar_RegenBinnen3Uur_StuurtNegatieveNotificatie()
    {
        // Arrange: only 1 dry hour, then rain
        var nu = DateTime.Today.AddHours(12);
        _ctx.Scheduler.AdvanceTo(nu.Ticks);
        SetupForecast(BuildForecast(nu, dryHours: 1, wetHoursAfter: 3));
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.wasmachine_washer_machine_state")
            .FromState("run")
            .ToState("stop");
        _ctx.HaContextMock.ProcessPendingOperations();
        await Task.Delay(200);

        // Assert: negative / warning notification
        _notify.Received().NotifyPeopleHome(
            Arg.Is<string>(t => t.Contains("niet buiten") || t.Contains("🌧️")),
            Arg.Any<string>(),
            true,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Any<List<ActionModel>?>(),
            image: Arg.Any<string?>(),
            channel: Arg.Any<string?>(),
            vibrationPattern: Arg.Any<string?>());
    }

    [Fact]
    public async Task WasmachineKlaar_DroogNietNu_MaarLaterTussenNegen_PlanNotificatieIn()
    {
        // Arrange: current time is 08:00, wet now, dry from 11:00
        var echtNu = DateTime.Today.AddHours(8);
        _ctx.Scheduler.AdvanceTo(echtNu.Ticks);

        // 3 wet hours (08:00-11:00), then 4 dry hours (11:00+)
        var forecastStart = echtNu;
        var entries = BuildForecast(forecastStart, dryHours: 0, wetHoursAfter: 3, dryHoursAfter: 5);
        SetupForecast(entries);

        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.wasmachine_washer_machine_state")
            .FromState("run")
            .ToState("stop");
        _ctx.HaContextMock.ProcessPendingOperations();
        await Task.Delay(200);

        // Assert: notification mentions a dry window and the time
        _notify.Received().NotifyPeopleHome(
            Arg.Is<string>(t => t.Contains("nu niet") || t.Contains("later") || t.Contains("venster") || t.Contains("🌧️")),
            Arg.Is<string>(m => m.Contains("11:00") || m.Contains("venster") || m.Contains("droog")),
            true,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Any<List<ActionModel>?>(),
            image: Arg.Any<string?>(),
            channel: Arg.Any<string?>(),
            vibrationPattern: Arg.Any<string?>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Tests: was hangt buiten → regen notificaties
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WasHangtBuiten_NeerslagSensorActief_StuurtWaarschuwing()
    {
        // Arrange: laundry is hanging outside
        SetWasHangtBuiten(true);
        var app = CreateApp();

        // Act: rain sensor turns on
        _ctx.ChangeStateFor("binary_sensor.weer_thuis_neerslag_verwacht")
            .FromState("off")
            .ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert: rain warning sent with "binnen gehaald" action button
        _notify.Received().NotifyPeopleHome(
            Arg.Is<string>(t => t.Contains("binnen") || t.Contains("regen") || t.Contains("🌧️")),
            Arg.Any<string>(),
            false,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Is<List<ActionModel>?>(a => a != null && a.Any(x => x.Title.Contains("binnen"))),
            image: Arg.Any<string?>(),
            channel: Arg.Any<string?>(),
            vibrationPattern: Arg.Any<string?>());
    }

    [Fact]
    public void WasHangtBuiten_NeerslagSensorActief_MaarWasAlBinnen_GeenNotificatie()
    {
        // Arrange: laundry is NOT outside
        SetWasHangtBuiten(false);
        var app = CreateApp();

        // Act: rain sensor fires anyway
        _ctx.ChangeStateFor("binary_sensor.weer_thuis_neerslag_verwacht")
            .FromState("off")
            .ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert: no notification
        _notify.DidNotReceive().NotifyPhoneVincent(
            Arg.Any<string>(),
            Arg.Is<string>(m => m.Contains("regen") || m.Contains("buiten")),
            Arg.Any<bool>(),
            Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Tests: zonsondergang
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Zonsondergang_WasHangtBuiten_StuurtNaarBinnenMelding()
    {
        // Arrange: laundry is outside
        SetWasHangtBuiten(true);

        // Let's say sunset is 2 hours from now
        // (using UTC ticks so Scheduler.Now.LocalDateTime lines up with "nu" regardless of the local time zone)
        var nu = DateTime.Today.AddHours(12);
        _ctx.Scheduler.AdvanceTo(nu.ToUniversalTime().Ticks);

        var app = CreateApp();

        var sunsetUtc = nu.AddHours(2).ToUniversalTime();
        
        // Act: change sensor state to trigger SetupSunsetMonitoring
        _ctx.ChangeStateFor("sensor.sun_next_setting")
            .FromState("unavailable")
            .ToState(sunsetUtc.ToString("O"));
        _ctx.HaContextMock.ProcessPendingOperations();

        // Advance 1 hour and 1 minute to the warning time (60 min before sunset)
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromMinutes(61).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert: sunset reminder sent
        _notify.Received().NotifyPeopleHome(
            Arg.Is<string>(t => t.Contains("binnen") || t.Contains("zonsondergang") || t.Contains("🌅")),
            Arg.Any<string>(),
            false,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Is<List<ActionModel>?>(a => a != null),
            image: Arg.Any<string?>(),
            channel: Arg.Any<string?>(),
            vibrationPattern: Arg.Any<string?>());
    }

    [Fact]
    public void Zonsondergang_WasNietBuiten_GeenMelding()
    {
        // Arrange: laundry is NOT outside
        SetWasHangtBuiten(false);
        var app = CreateApp();
        
        var nu = DateTime.Today.AddHours(12);
        _ctx.Scheduler.AdvanceTo(nu.Ticks);
        
        var sunsetUtc = nu.AddHours(2).ToUniversalTime();

        // Act
        _ctx.ChangeStateFor("sensor.sun_next_setting")
            .FromState("unavailable")
            .ToState(sunsetUtc.ToString("O"));
        _ctx.HaContextMock.ProcessPendingOperations();

        // Advance 1 hour to the warning time
        _ctx.Scheduler.AdvanceBy(TimeSpan.FromHours(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert: no notification
        _notify.DidNotReceive().NotifyPhoneVincent(
            Arg.Is<string>(t => t.Contains("🌅") || t.Contains("zon")),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Tests: toggle monitoring lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WasHangtBuitenToggle_WanneerUitgezet_StoptRegenNotificaties()
    {
        // Arrange: was hangt buiten
        SetWasHangtBuiten(true);
        var app = CreateApp();

        // Turn off (laundry brought in)
        SetWasHangtBuiten(false);
        _ctx.ChangeStateFor("input_boolean.laundryhangingoutside")
            .FromState("on")
            .ToState("off");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Act: rain sensor fires after laundry is inside
        _ctx.ChangeStateFor("binary_sensor.weer_thuis_neerslag_verwacht")
            .FromState("off")
            .ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert: no rain notification since laundry is inside
        _notify.DidNotReceive().NotifyPhoneVincent(
            Arg.Is<string>(t => t.Contains("🌧️")),
            Arg.Is<string>(m => m.Contains("regen") || m.Contains("regenen")),
            false,
            Arg.Any<double?>(),
            Arg.Any<List<ActionModel>?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>());
    }
}
