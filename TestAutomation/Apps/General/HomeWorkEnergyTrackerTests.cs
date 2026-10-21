using System.IO;
using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.Energy;
using Automation.Repository;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HomeWorkEnergyTrackerTests : IDisposable
{
    private const string Import = "sensor.p1_meter_3c39e72a64e8_total_power_import";
    private const string Export = "sensor.p1_meter_3c39e72a64e8_total_power_export";
    private const string Washer = "sensor.badkamer_wasmachine_energie";
    private const string Power = "sensor.p1_meter_3c39e72a64e8_active_power";

    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"HomeWorkEnergyTrackerTests_{Guid.NewGuid():N}");
    private readonly IDataRepository _storage;

    public HomeWorkEnergyTrackerTests()
    {
        Directory.CreateDirectory(_storagePath);
        _storage = new DataRepository(_storagePath, Substitute.For<ILogger>());
        _entityManager.EntityExists(HomeWorkEnergyTracker.SensorId).Returns(true);

        // Tuesday 20 October 2026, 10:00 Dutch time: a home work day
        _ctx.SetCurrentTime(new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc));
        SetState("input_boolean.officeday_tuesday", "off");
        SetState("input_boolean.holliday", "off");
        SetState("input_boolean.onvacation", "off");
        SetState("input_boolean.awayvincent", "off");
        SetState("input_boolean.workingvincent", "off");
        SetState(Import, "1000.000");
        SetState(Export, "500.000");
        SetState(Washer, "690.0");
        SetState(Power, "300");
        SetState("sensor.anwb_electricity_all_in_price_current", "0.40");
        SetState("sensor.anwb_electricity_market_price_current", "0.10");

        _ctx.InitApp<HomeWorkEnergyTracker>(Options.Create(new AppConfig()), _storage, _entityManager);
    }

    public void Dispose() => Directory.Delete(_storagePath, recursive: true);

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    private void StartWorking() => _ctx.ChangeStateFor("input_boolean.workingvincent").FromState("off").ToState("on");
    private void StopWorking() => _ctx.ChangeStateFor("input_boolean.workingvincent").FromState("on").ToState("off");

    private HomeWorkDay SavedDay() => _storage.Get<List<HomeWorkDay>>("HomeWorkEnergyDays")!.Single(x => x.Date == "2026-10-20");

    [Fact]
    public void Import_WhileWorkingFromHome_IsCountedAtTheAllInPrice()
    {
        StartWorking();

        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1002.000");
        StopWorking();

        var day = SavedDay();
        day.ImportKwh.Should().BeApproximately(2, 0.0001);
        day.NetCost.Should().BeApproximately(0.80, 0.0001);
    }

    [Fact]
    public void Export_IsSubtractedAtTheMarketPrice()
    {
        StartWorking();

        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1002.000");
        _ctx.ChangeStateFor(Export).FromState("500.000").ToState("503.000");
        StopWorking();

        var day = SavedDay();
        day.NetKwh.Should().BeApproximately(-1, 0.0001);
        day.NetCost.Should().BeApproximately(2 * 0.40 - 3 * 0.10, 0.0001);
    }

    [Fact]
    public void Washer_IsLeftOut_AtTheAllInPrice_WhenTakenFromTheGrid()
    {
        StartWorking();

        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1002.000");
        _ctx.ChangeStateFor(Washer).FromState("690.0").ToState("690.5");
        StopWorking();

        var day = SavedDay();
        day.NetKwh.Should().BeApproximately(1.5, 0.0001);
        day.NetCost.Should().BeApproximately(1.5 * 0.40, 0.0001);
    }

    [Fact]
    public void Washer_IsLeftOut_AtTheMarketPrice_WhenRunningOnOwnSolarPower()
    {
        StartWorking();
        SetState(Power, "-800");

        _ctx.ChangeStateFor(Export).FromState("500.000").ToState("502.000");
        _ctx.ChangeStateFor(Washer).FromState("690.0").ToState("690.5");
        StopWorking();

        // Without the washer we would have fed back 0.5 kWh more
        SavedDay().NetCost.Should().BeApproximately(-2 * 0.10 - 0.5 * 0.10, 0.0001);
    }

    [Fact]
    public void Energy_OutsideWorkingHours_IsNotCounted()
    {
        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1005.000");
        StartWorking();
        StopWorking();

        SavedDay().ImportKwh.Should().Be(0);
    }

    [Fact]
    public void Energy_OnAnOfficeDay_IsNotCounted()
    {
        SetState("input_boolean.officeday_tuesday", "on");
        StartWorking();

        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1002.000");

        _storage.Get<List<HomeWorkDay>>("HomeWorkEnergyDays").Should().BeNull();
    }

    [Fact]
    public void Energy_WhileVincentIsAway_IsNotCounted()
    {
        StartWorking();
        _ctx.ChangeStateFor("input_boolean.awayvincent").FromState("off").ToState("on");

        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1002.000");

        SavedDay().ImportKwh.Should().Be(0);
    }

    [Fact]
    public void WorkingMinutes_AreCounted()
    {
        StartWorking();
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(90).Ticks);
        StopWorking();

        SavedDay().Minutes.Should().BeApproximately(90, 0.01);
    }

    [Fact]
    public void Publish_ShowsTodayAndTheDays()
    {
        StartWorking();
        _ctx.ChangeStateFor(Import).FromState("1000.000").ToState("1002.000");
        StopWorking();

        var call = _entityManager.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IEntityManager.SetState));
        call.GetArguments()[1].Should().Be(0.8);
        var attributes = JsonSerializer.SerializeToElement(call.GetArguments()[2]);
        attributes.GetProperty("dagen")[0].GetProperty("datum").GetString().Should().Be("2026-10-20");
        attributes.GetProperty("maand_kosten").GetDouble().Should().Be(0.8);
    }
}
