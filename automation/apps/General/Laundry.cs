using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Laundry;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Manages laundry drying notifications by coupling the washing machine state to weather forecasts.
/// </summary>
[NetDaemonApp(Id = nameof(Laundry))]
public class Laundry : BaseApp
{
    private const int RequiredDryHours = 3;
    private const int StartWindowHour = 9;   // 09:00
    private const int EndWindowHour = 19;   // 19:00
    
    private IDisposable? _hourlyRainCheckSub;
    private IDisposable? _rainSensorSub;
    private IDisposable? _nextChanceSchedule;
    private IDisposable? _sunsetSchedule;
    private IDisposable? _sunSensorSub;

    public Laundry(
        IHaContext ha,
        ILogger<Laundry> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler)
    {
        SetupWashingMachineMonitoring();
        SetupLaundryOutsideToggleMonitoring();
    }

    private void SetupWashingMachineMonitoring()
    {
        Entities.Sensor.WasmachineWasherMachineState
            .StateChanges()
            .Where(x => x.Old?.State?.ToLower() is "run" or "running" &&
                        x.New?.State?.ToLower() is "stop" or "stopped" or "end")
            .Subscribe(stateChange =>
            {
                try
                {
                    Logger.LogInformation("Washing machine finished, checking weather forecast");
                    StartLaundryOutsideMonitoring();
                    _ = Task.Run(OnWashingMachineFinishedAsync);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing washing machine finished event");
                }
            });
    }

    private void SetupLaundryOutsideToggleMonitoring()
    {
        Entities.InputBoolean.Laundryhangingoutside.WhenTurnsOn(_ =>
        {
            Logger.LogInformation("Laundry is outside, started monitoring");
            StartLaundryOutsideMonitoring();
        });

        Entities.InputBoolean.Laundryhangingoutside.WhenTurnsOff(_ =>
        {
            Logger.LogInformation("Laundry brought inside, stopped monitoring");
            StopLaundryOutsideMonitoring();
        });
    }

    private async Task OnWashingMachineFinishedAsync()
    {
        try
        {
            var forecast = await GetHourlyForecastAsync();
            if (forecast == null || forecast.Count == 0)
            {
                Logger.LogWarning("No weather forecast available");
                Notify.NotifyPeopleHome(
                    "🧺 Wasmachine klaar",
                    "De wasmachine is klaar, maar er is geen weersverwachting beschikbaar.",
                    true);
                return;
            }

            var now = Scheduler.Now.LocalDateTime;
            var dryResult = CalculateDryWindow(forecast, now);

            SendWashingMachineFinishedNotification(dryResult, now);

            if (dryResult is { IsDryNow: false, NextWindowTime: not null })
                ScheduleNextChanceNotification(dryResult.NextWindowTime.Value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing washing machine finished");
        }
    }

    private async Task<List<ForecastEntry>?> GetHourlyForecastAsync()
    {
        try
        {
            var result = await Entities.Weather.WeerThuis.GetForecastsAsync(
                new { type = "hourly" });

            if (result == null) return null;

            if (result.Value.TryGetProperty(Entities.Weather.WeerThuis.EntityId, out var entityProp) &&
                entityProp.TryGetProperty("forecast", out var forecastProp))
                return forecastProp.Deserialize<List<ForecastEntry>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching weather forecast");
            return null;
        }
    }

    private DryWindowResult CalculateDryWindow(List<ForecastEntry> forecast, DateTime fromNow)
    {
        var futureForecast = forecast
            .Where(f => f.DateTime.ToLocalTime() >= fromNow.AddMinutes(-30))
            .OrderBy(f => f.DateTime)
            .ToList();

        var dryUntil = GetRainStartTime(futureForecast, fromNow);
        var dryHours = dryUntil.HasValue
            ? (int)(dryUntil.Value - fromNow).TotalHours
            : CountConsecutiveDryHours(futureForecast, 0);

        var isDryNow = dryHours >= RequiredDryHours;

        DateTime? nextWindowTime = null;

        if (!isDryNow)
        {
            var today = fromNow.Date;
            var windowStart = today.AddHours(StartWindowHour);
            var windowEnd = today.AddHours(EndWindowHour);

            for (var hour = fromNow.AddHours(1); hour < windowEnd; hour = hour.AddHours(1))
            {
                if (hour < windowStart) continue;

                var indexFromHour = futureForecast
                    .FindIndex(f => f.DateTime.ToLocalTime() >= hour.AddMinutes(-30));

                if (indexFromHour < 0) break;

                var dryFromHour = CountConsecutiveDryHours(futureForecast, indexFromHour);
                if (dryFromHour >= RequiredDryHours)
                {
                    nextWindowTime = hour;
                    break;
                }
            }
        }

        string? dryUntilDescription = null;
        if (isDryNow)
        {
            if (!dryUntil.HasValue)
                dryUntilDescription = "de rest van de dag";
            else
                dryUntilDescription = $"{(int)(dryUntil.Value - fromNow).TotalHours} uur";
        }

        return new DryWindowResult(isDryNow, dryUntil, dryUntilDescription, nextWindowTime);
    }

    private static DateTime? GetRainStartTime(List<ForecastEntry> forecast, DateTime fromNow)
    {
        foreach (var entry in forecast.Where(f => f.DateTime.ToLocalTime() >= fromNow.AddMinutes(-30)))
            if (IsWet(entry)) return entry.DateTime.ToLocalTime();
        return null;
    }

    private static int CountConsecutiveDryHours(List<ForecastEntry> forecast, int startIndex)
    {
        var count = 0;
        for (var i = startIndex; i < forecast.Count; i++)
        {
            if (IsWet(forecast[i])) break;
            count++;
        }
        return count;
    }

    private static bool IsWet(ForecastEntry entry) => entry.Precipitation > 0;

    private void SendWashingMachineFinishedNotification(DryWindowResult result, DateTime now)
    {
        string title;
        string message;
        List<ActionModel>? actions = null;

        if (result.IsDryNow)
        {
            title = "🧺☀️ Wasmachine klaar – was kan buiten!";
            var dryInfo = result.DryUntilDescription == "de rest van de dag"
                ? "Het blijft de rest van de dag droog."
                : $"Het blijft nog {result.DryUntilDescription} droog.";

            message = $"De wasmachine is klaar. {dryInfo}";

            actions =
            [
                new ActionModel("HANG_WAS_BUITEN", "✅ Was buiten gehangen", func: OnLaundryHangingOutside),
            ];
        }
        else if (result.NextWindowTime.HasValue)
        {
            var hourStr = result.NextWindowTime.Value.ToString("HH:mm");
            title = "🧺🌧️ Wasmachine klaar – nu niet buiten";
            message = $"De wasmachine is klaar, maar het is nu te nat. " +
                      $"Tussen {hourStr} en {EndWindowHour}:00 is er een droog venster. Je krijgt een melding.";
        }
        else
        {
            title = "🧺🌧️ Wasmachine klaar – niet buiten vandaag";
            message = "De wasmachine is klaar, maar het wordt vandaag niet droog genoeg " +
                      "om de was buiten te hangen (minimaal 3 uur droog vereist).";
        }

        Notify.NotifyPeopleHome(title, message, true, action: actions);
        Logger.LogInformation("Sent washing machine finished notification: IsDryNow={IsDryNow}", result.IsDryNow);
    }

    private void ScheduleNextChanceNotification(DateTime nextChanceTime)
    {
        _nextChanceSchedule?.Dispose();

        var now = Scheduler.Now.LocalDateTime;
        var delay = nextChanceTime - now;

        if (delay <= TimeSpan.Zero) return;
        if (nextChanceTime.Hour >= EndWindowHour) return;

        Logger.LogInformation("Scheduled next chance notification for {Time}", nextChanceTime);

        _nextChanceSchedule = Scheduler.Schedule(delay, async () =>
        {
            try
            {
                var forecast = await GetHourlyForecastAsync();
                var now2 = Scheduler.Now.LocalDateTime;
                if (forecast != null && forecast.Count > 0)
                {
                    var result = CalculateDryWindow(forecast, now2);
                    if (result.IsDryNow)
                    {
                        var dryInfo = result.DryUntilDescription == "de rest van de dag"
                            ? "Het blijft de rest van de dag droog."
                            : $"Het blijft nog {result.DryUntilDescription} droog.";

                        Notify.NotifyPeopleHome(
                            "🧺☀️ Was kan nu buiten!",
                            $"Nu is er een droog venster. {dryInfo}",
                            true,
                            action:
                            [
                                new ActionModel("HANG_WAS_BUITEN", "✅ Was buiten gehangen",
                                    func: OnLaundryHangingOutside),
                            ]);
                    }
                    else
                    {
                        Notify.NotifyPeopleHome(
                            "🧺🌧️ Was toch niet buiten",
                            "Het venster dat verwacht werd is toch te nat geworden. Nog even wachten.",
                            true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in next chance notification");
            }
        });
    }

    private void OnLaundryHangingOutside()
    {
        try
        {
            Logger.LogInformation("User confirmed: laundry is hanging outside");
            Entities.InputBoolean.Laundryhangingoutside.TurnOn();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error turning on laundryhangingoutside");
        }
    }

    private void OnLaundryBroughtInside()
    {
        try
        {
            Logger.LogInformation("User confirmed: laundry brought inside");
            Entities.InputBoolean.Laundryhangingoutside.TurnOff();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error turning off laundryhangingoutside");
        }
    }

    private void StartLaundryOutsideMonitoring()
    {
        StopLaundryOutsideMonitoring();

        // Check hourly using Scheduler.RunEvery (instead of cron)
        _hourlyRainCheckSub = Scheduler.SchedulePeriodic(TimeSpan.FromHours(1), () =>
        {
            if (!IsLaundryOutside()) return;
            try
            {
                _ = Task.Run(CheckHourlyRainAsync);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in hourly rain check");
            }
        });

        // Real-time rain sensor
        _rainSensorSub = Entities.BinarySensor.WeerThuisNeerslagVerwacht
            .StateChanges()
            .Where(x => x.New?.State == "on")
            .Subscribe(_ =>
            {
                if (!IsLaundryOutside()) return;
                try
                {
                    Logger.LogInformation("Rain sensor active, sending laundry warning");
                    SendLaundryRainWarning(isRealtimeSensor: true);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error sending rain warning");
                }
            });

        // Setup sunset monitoring
        SetupSunsetMonitoring();
        _sunSensorSub = Entities.Sensor.SunNextSetting
            .StateChanges()
            .Subscribe(_ => SetupSunsetMonitoring());

        Logger.LogInformation("Laundry outside monitoring started");
    }

    private void StopLaundryOutsideMonitoring()
    {
        _hourlyRainCheckSub?.Dispose();
        _hourlyRainCheckSub = null;

        _rainSensorSub?.Dispose();
        _rainSensorSub = null;

        _sunsetSchedule?.Dispose();
        _sunsetSchedule = null;

        _sunSensorSub?.Dispose();
        _sunSensorSub = null;

        Logger.LogInformation("Laundry outside monitoring stopped");
    }

    private async Task CheckHourlyRainAsync()
    {
        try
        {
            if (!IsLaundryOutside()) return;

            var forecast = await GetHourlyForecastAsync();
            if (forecast == null || forecast.Count == 0) return;

            var thisHour = Scheduler.Now.LocalDateTime;
            var thisHourEntry = forecast
                .FirstOrDefault(f => {
                    var localF = f.DateTime.ToLocalTime();
                    return localF.Year == thisHour.Year &&
                           localF.Month == thisHour.Month &&
                           localF.Day == thisHour.Day &&
                           localF.Hour == thisHour.Hour;
                });

            if (thisHourEntry != null && IsWet(thisHourEntry))
            {
                Logger.LogInformation(
                    "Hourly check: rain expected this hour ({Hour}:00), sending notification",
                    thisHour.Hour);
                SendLaundryRainWarning(isRealtimeSensor: false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in hourly rain check");
        }
    }

    private void SendLaundryRainWarning(bool isRealtimeSensor)
    {
        var message = isRealtimeSensor
            ? "Het begint te regenen! Haal de was snel naar binnen."
            : "Er wordt regen verwacht dit uur. Haal de was naar binnen.";

        Notify.NotifyPeopleHome(
            "🌧️ Was naar binnen!",
            message,
            false,
            sendAfterMinutes: 30,
            action:
            [
                new ActionModel("WAS_BINNEN_GEHAALD", "✅ Was binnen gehaald",
                    func: OnLaundryBroughtInside)
            ]);
    }

    private void SetupSunsetMonitoring()
    {
        _sunsetSchedule?.Dispose();

        if (!IsLaundryOutside()) return;

        try
        {
            var sunsetTimeStr = Entities.Sensor.SunNextSetting.State;
            if (string.IsNullOrEmpty(sunsetTimeStr) || !DateTime.TryParse(sunsetTimeStr, out var sunsetUtc))
                return;

            var sunsetLocal = sunsetUtc.ToLocalTime();
            
            // Schedule the notification 60 minutes before sunset
            var warningTime = sunsetLocal.AddMinutes(-60);
            var delay = warningTime - Scheduler.Now.LocalDateTime;

            if (delay <= TimeSpan.Zero)
            {
                // If it's already less than 60 mins until sunset, and sunset hasn't passed, notify immediately
                if (sunsetLocal > Scheduler.Now.LocalDateTime)
                {
                    Logger.LogInformation("Sunset is imminent, sending immediate notification");
                    SendSunsetWarning(sunsetLocal);
                }
                return;
            }

            Logger.LogInformation("Scheduled sunset warning for {WarningTime}", warningTime);
            
            _sunsetSchedule = Scheduler.Schedule(delay, () =>
            {
                if (!IsLaundryOutside()) return;
                SendSunsetWarning(sunsetLocal);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error setting up sunset monitoring");
        }
    }

    private void SendSunsetWarning(DateTime sunsetTime)
    {
        Notify.NotifyPeopleHome(
            "🌅 Was naar binnen",
            $"De zon gaat onder om {sunsetTime:HH:mm}. Haal de was naar binnen!",
            false,
            sendAfterMinutes: 60,
            action:
            [
                new ActionModel("WAS_BINNEN_GEHAALD", "✅ Was binnen gehaald",
                    func: OnLaundryBroughtInside)
            ]);
    }

    private bool IsLaundryOutside()
    {
        return Entities.InputBoolean.Laundryhangingoutside.IsOn();
    }
}