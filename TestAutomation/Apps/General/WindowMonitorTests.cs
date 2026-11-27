using System.Text.Json;
using Automation.apps.General.Household;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Repository;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class WindowMonitorTests : IDisposable
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"WindowsTests_{Guid.NewGuid():N}");
    private readonly IDataRepository _storage;

    public WindowMonitorTests()
    {
        Directory.CreateDirectory(_storagePath);
        _storage = new DataRepository(_storagePath, Substitute.For<ILogger>());
        _entityManager.EntityExists(Arg.Any<string>()).Returns(true);

        // People home (the phone messages go to them), a mild dry afternoon outside and the house at 23° and 57%
        Set("input_boolean.awayvincent", "off");
        Set("input_boolean.awaycarleen", "off");
        foreach (var (key, _) in WindowMonitor.WindowList) Set(WindowMonitor.Helper(key), "off");
        Weather();
        Set("sensor.luchtontvochtiger_temperatuur", "23");
        Set("sensor.luchtontvochtiger_luchtvochtigheid", "57");
        Set("sun.sun", "above_horizon");
    }

    public void Dispose() => Directory.Delete(_storagePath, recursive: true);

    private static DateTime Utc(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    private void Set(string entityId, string state, DateTime? lastChanged = null) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state, LastChanged = lastChanged });

    private void Weather(string temperature = "12", string humidity = "70", string gust = "20", bool rain = false)
    {
        Set("sensor.weer_thuis_temperatuur", temperature);
        Set("sensor.weer_thuis_luchtvochtigheid", humidity);
        Set("sensor.weer_thuis_windstoten", gust);
        Set("binary_sensor.weer_thuis_neerslag_verwacht", rain ? "on" : "off");
        Set("binary_sensor.weer_thuis_waarschuwing_actief", "off");
    }

    /// <summary>Starts the app on a Dutch winter day (UTC+1) at the given UTC time.</summary>
    private WindowMonitor Start(DateTime at)
    {
        _ctx.SetCurrentTime(at);
        var app = _ctx.InitApp<WindowMonitor>(Options.Create(new AppConfig()), _storage, _entityManager);
        _ctx.HaContextMock.ProcessPendingOperations();
        return app;
    }

    private void Advance(TimeSpan time)
    {
        _ctx.AdvanceTimeBy(time.Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();
        Thread.Sleep(30);
    }

    private void Open(string room, DateTime at)
    {
        var old = new EntityState { EntityId = WindowMonitor.Helper(room), State = "off", LastChanged = at.AddHours(-3) };
        var @new = new EntityState { EntityId = WindowMonitor.Helper(room), State = "on", LastChanged = at };
        Emit(room, old, @new);
    }

    private void Close(string room, DateTime openedAt)
    {
        var old = new EntityState { EntityId = WindowMonitor.Helper(room), State = "on", LastChanged = openedAt };
        var @new = new EntityState { EntityId = WindowMonitor.Helper(room), State = "off", LastChanged = _ctx.Scheduler.Now.UtcDateTime };
        Emit(room, old, @new);
    }

    private void Emit(string room, EntityState old, EntityState @new)
    {
        var id = WindowMonitor.Helper(room);
        _ctx.HaContext.GetState(id).Returns(@new);
        _ctx.HaContextMock.StateChangeSubject.OnNext(new StateChange(new Entity(_ctx.HaContext, id), old, @new));
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    /// <summary>The state and the attributes the app published last for a sensor.</summary>
    private (string State, JsonElement Attributes) Last(string sensor)
    {
        var call = _entityManager.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "SetState" && (string)c.GetArguments()[0]! == sensor)
            .Last();
        var args = call.GetArguments();
        return (Convert.ToString(args[1], System.Globalization.CultureInfo.InvariantCulture)!, JsonSerializer.SerializeToElement(args[2]));
    }

    private void VerifyMessages(int times) => _ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: times);

    [Fact]
    public void AnOpenWindow_IsPublished_WithSinceWhen()
    {
        Start(Utc(26, 9));

        Open("woonkamer_tv", Utc(26, 9));

        var (state, attributes) = Last(WindowMonitor.OpenSensor);
        state.Should().Be("1");
        var window = attributes.GetProperty("ramen").EnumerateArray().Single();
        window.GetProperty("sleutel").GetString().Should().Be("woonkamer_tv");
        window.GetProperty("raam").GetString().Should().Be("Woonkamer bij de tv");
        DateTimeOffset.Parse(window.GetProperty("sinds").GetString()!).UtcDateTime.Should().Be(Utc(26, 9));
    }

    [Fact]
    public void WithoutOpenWindows_TheCountIsZero()
    {
        Start(Utc(26, 9));

        Last(WindowMonitor.OpenSensor).State.Should().Be("0");
        Last(WindowMonitor.TodaySensor).State.Should().Be("0");
    }

    [Fact]
    public void ACosedWindow_AddsItsTimeToToday()
    {
        Start(Utc(26, 9));
        Open("woonkamer_tv", Utc(26, 9));
        Advance(TimeSpan.FromMinutes(45));

        Close("woonkamer_tv", Utc(26, 9));

        var (state, attributes) = Last(WindowMonitor.TodaySensor);
        state.Should().Be("45");
        attributes.GetProperty("per_raam").GetProperty("woonkamer_tv").GetDouble().Should().Be(45);
        attributes.GetProperty("per_raam").GetProperty("woonkamer_tafel").GetDouble().Should().Be(0);
        attributes.GetProperty("langste_minuten").GetDouble().Should().Be(45);
        Last(WindowMonitor.OpenSensor).State.Should().Be("0");
    }

    [Fact]
    public void SeveralOpenings_AreAddedUp_AndTheLongestIsKept()
    {
        Start(Utc(26, 8));
        Open("woonkamer_tafel", Utc(26, 8));
        Advance(TimeSpan.FromMinutes(20));
        Close("woonkamer_tafel", Utc(26, 8));
        Advance(TimeSpan.FromMinutes(100));
        Open("slaapkamer", Utc(26, 10));
        Advance(TimeSpan.FromMinutes(60));
        Close("slaapkamer", Utc(26, 10));

        var (state, attributes) = Last(WindowMonitor.TodaySensor);
        state.Should().Be("80");
        attributes.GetProperty("per_raam").GetProperty("woonkamer_tafel").GetDouble().Should().Be(20);
        attributes.GetProperty("per_raam").GetProperty("slaapkamer").GetDouble().Should().Be(60);
        attributes.GetProperty("langste_minuten").GetDouble().Should().Be(60);
    }

    [Fact]
    public void TheTwoLivingRoomWindows_AreTrackedSeparately()
    {
        Start(Utc(26, 9));
        Open("woonkamer_tv", Utc(26, 9));
        Open("woonkamer_tafel", Utc(26, 9));
        Last(WindowMonitor.OpenSensor).State.Should().Be("2");

        Advance(TimeSpan.FromMinutes(40));
        Close("woonkamer_tv", Utc(26, 9));

        // The one by the table is still open: counted live, the one by the tv is done
        Last(WindowMonitor.OpenSensor).State.Should().Be("1");
        Last(WindowMonitor.OpenSensor).Attributes.GetProperty("ramen").EnumerateArray().Single().GetProperty("sleutel").GetString().Should().Be("woonkamer_tafel");
        var today = Last(WindowMonitor.TodaySensor).Attributes.GetProperty("per_raam");
        today.GetProperty("woonkamer_tv").GetDouble().Should().Be(40);
        today.GetProperty("woonkamer_tafel").GetDouble().Should().Be(40);
        today.GetProperty("slaapkamer").GetDouble().Should().Be(0);
    }

    [Fact]
    public void AWindowThatIsStillOpen_CountsForToday_WhileItIsOpen()
    {
        Start(Utc(26, 9));
        Open("woonkamer_tv", Utc(26, 9));

        Advance(TimeSpan.FromMinutes(30));

        Last(WindowMonitor.TodaySensor).State.Should().Be("30");
        Last(WindowMonitor.OpenSensor).Attributes.GetProperty("ramen").EnumerateArray().Single().GetProperty("minuten").GetDouble().Should().Be(30);
    }

    [Fact]
    public void AWindowOpenOverMidnight_IsSplitOverTheDays()
    {
        // 23:30 until 00:20 Dutch time
        Start(Utc(26, 22, 30));
        Open("woonkamer_tv", Utc(26, 22, 30));
        Advance(TimeSpan.FromMinutes(50));

        Close("woonkamer_tv", Utc(26, 22, 30));

        var (state, attributes) = Last(WindowMonitor.TodaySensor);
        state.Should().Be("20");
        attributes.GetProperty("langste_minuten").GetDouble().Should().Be(50);
        var yesterday = attributes.GetProperty("week").EnumerateArray().Single();
        yesterday.GetProperty("datum").GetString().Should().Be("2026-11-26");
        yesterday.GetProperty("minuten").GetDouble().Should().Be(30);
    }

    [Fact]
    public void WhatWasOpen_SurvivesARestart()
    {
        Start(Utc(26, 9));
        Open("woonkamer_tv", Utc(26, 9));
        Advance(TimeSpan.FromMinutes(45));
        Close("woonkamer_tv", Utc(26, 9));

        // A new instance of the app (NetDaemon restarted) reads it back
        var restarted = new WindowMonitorTests();
        try
        {
            File.Copy(Path.Combine(_storagePath, "WindowsDays_store.json"), Path.Combine(restarted._storagePath, "WindowsDays_store.json"));
            restarted.Start(Utc(26, 12));
            restarted.Last(WindowMonitor.TodaySensor).State.Should().Be("45");
        }
        finally
        {
            restarted.Dispose();
        }
    }

    [Fact]
    public void TheAdvice_IsPublishedWithItsReasons()
    {
        Weather(temperature: "6", humidity: "95");
        Start(Utc(26, 9));

        var (state, attributes) = Last(WindowMonitor.AdviceSensor);

        state.Should().Be("Weinig nut");
        attributes.GetProperty("sleutel").GetString().Should().Be("weinig_nut");
        attributes.GetProperty("redenen").EnumerateArray().Select(r => r.GetString()).Should().Contain(r => r!.Contains("95% vochtig"));
    }

    [Fact]
    public void TheAdvice_FollowsTheWeather()
    {
        Start(Utc(26, 9));
        Last(WindowMonitor.AdviceSensor).State.Should().Be("Luchten kan");

        Weather(rain: true);
        _ctx.HaContextMock.StateChangeSubject.OnNext(new StateChange(
            new Entity(_ctx.HaContext, "binary_sensor.weer_thuis_neerslag_verwacht"),
            new EntityState { EntityId = "binary_sensor.weer_thuis_neerslag_verwacht", State = "off" },
            new EntityState { EntityId = "binary_sensor.weer_thuis_neerslag_verwacht", State = "on" }));
        Advance(TimeSpan.FromSeconds(5));

        Last(WindowMonitor.AdviceSensor).State.Should().Be("Liever dicht");
    }

    [Fact]
    public void AnOpenWindow_WhenRainIsExpected_SendsOneMessage()
    {
        Weather(rain: true);
        Start(Utc(26, 9));
        Open("woonkamer_tafel", Utc(26, 9));

        // The clock looks again every minute: still one message
        Advance(TimeSpan.FromMinutes(5));

        VerifyMessages(1);
    }

    [Fact]
    public void ClosedWindows_SendNoMessages()
    {
        Weather(rain: true);
        Start(Utc(26, 9));

        Advance(TimeSpan.FromMinutes(5));

        VerifyMessages(0);
    }

    [Fact]
    public void AWindowThatIsClosedAndOpenedAgain_WarnsAgain()
    {
        Weather(rain: true);
        Start(Utc(26, 9));
        Open("woonkamer_tafel", Utc(26, 9));
        Advance(TimeSpan.FromMinutes(5));
        Close("woonkamer_tafel", Utc(26, 9));

        Open("woonkamer_tafel", Utc(26, 10));
        Advance(TimeSpan.FromMinutes(5));

        VerifyMessages(2);
    }

    [Fact]
    public void AWindowOpenForTooLong_SendsAMessage()
    {
        Start(Utc(26, 9));
        Open("woonkamer_tv", Utc(26, 9));

        Advance(WindowMonitor.LongOpen - TimeSpan.FromMinutes(1));
        VerifyMessages(0);

        Advance(TimeSpan.FromMinutes(2));
        VerifyMessages(1);
    }

    [Fact]
    public void WhenItIsCold_AWindowIsOpenTooLongSooner()
    {
        Weather(temperature: "2", humidity: "70");
        Start(Utc(26, 9));
        Open("woonkamer_tv", Utc(26, 9));

        Advance(WindowMonitor.LongOpenCold + TimeSpan.FromMinutes(1));

        VerifyMessages(1);
    }

    [Fact]
    public void InTheDarkAndWarm_AnOpenWindowWarnsAboutBugs()
    {
        Weather(temperature: "14", humidity: "60");
        Set("sun.sun", "below_horizon");
        Start(Utc(26, 18));
        Open("slaapkamer", Utc(26, 18));

        Advance(TimeSpan.FromMinutes(2));

        VerifyMessages(1);
    }

    [Fact]
    public void InTheDarkAndCold_AnOpenWindowWarnsOnce()
    {
        Weather(temperature: "-3", humidity: "80");
        Set("sun.sun", "below_horizon");
        Start(Utc(26, 22));
        Open("woonkamer_tv", Utc(26, 22));

        // The clock looks again every minute: still one message
        Advance(TimeSpan.FromMinutes(10));

        VerifyMessages(1);
    }

    [Fact]
    public void WhenBothAreAway_TheAdviceSaysNobodyIsHome()
    {
        Set("input_boolean.awayvincent", "on");
        Set("input_boolean.awaycarleen", "on");
        Start(Utc(26, 11));

        var (state, attributes) = Last(WindowMonitor.AdviceSensor);

        state.Should().Be("Niemand thuis");
        attributes.GetProperty("sleutel").GetString().Should().Be("weg");
    }

    [Fact]
    public void WhenOnlyOneIsAway_SomeoneIsStillHome()
    {
        Set("input_boolean.awayvincent", "on");
        Start(Utc(26, 11));

        Last(WindowMonitor.AdviceSensor).State.Should().Be("Luchten kan");
    }

    [Fact]
    public void WhenTheLastOneLeaves_TheAdviceChangesRightAway()
    {
        Set("input_boolean.awayvincent", "on");
        Start(Utc(26, 11));
        Last(WindowMonitor.AdviceSensor).State.Should().Be("Luchten kan");

        Set("input_boolean.awaycarleen", "on");
        _ctx.HaContextMock.StateChangeSubject.OnNext(new StateChange(
            new Entity(_ctx.HaContext, "input_boolean.awaycarleen"),
            new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" },
            new EntityState { EntityId = "input_boolean.awaycarleen", State = "on" }));
        Advance(TimeSpan.FromSeconds(5));

        Last(WindowMonitor.AdviceSensor).State.Should().Be("Niemand thuis");
    }

    [Fact]
    public void AnOpenWindow_WhileNobodyIsHome_SendsOneMessage()
    {
        Set("input_boolean.awayvincent", "on");
        Set("input_boolean.awaycarleen", "on");
        Start(Utc(26, 11));
        Open("woonkamer_tv", Utc(26, 11));

        Advance(TimeSpan.FromMinutes(10));

        VerifyMessages(1);
    }

    [Fact]
    public void AtNight_TheAdviceIsToKeepTheWindowsClosed()
    {
        // The night of 26 to 27 November: -3° and 80% outside, dark, and the air outside is far drier than inside
        Weather(temperature: "-3", humidity: "80");
        Set("sun.sun", "below_horizon");
        Start(Utc(26, 23));

        var (state, attributes) = Last(WindowMonitor.AdviceSensor);

        state.Should().Be("Liever dicht");
        attributes.GetProperty("sleutel").GetString().Should().Be("dicht");
        attributes.GetProperty("redenen").EnumerateArray().Select(r => r.GetString()).Should().Contain(r => r!.Contains("Het is donker"));
    }
}
