using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Repository;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class CatVomitTrackerTests : IDisposable
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"CatVomitTrackerTests_{Guid.NewGuid():N}");
    private readonly IDataRepository _storage;

    public CatVomitTrackerTests()
    {
        Directory.CreateDirectory(_storagePath);
        _storage = new DataRepository(_storagePath, Substitute.For<ILogger>());
        _entityManager.EntityExists(CatVomitTracker.SensorId).Returns(true);
        // Tuesday 20 October 2026, 10:00 Dutch time (summer time, UTC+2)
        _ctx.SetCurrentTime(new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc));
        _ctx.InitApp<CatVomitTracker>(Options.Create(new AppConfig()), _storage, _entityManager);
    }

    public void Dispose() => Directory.Delete(_storagePath, recursive: true);

    /// <summary>The moments of the last state that was published for the sensor.</summary>
    private List<string> PublishedMoments()
    {
        var call = _entityManager.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IEntityManager.SetState));
        var attributes = JsonSerializer.SerializeToElement(call.GetArguments()[2]);
        return attributes.GetProperty("momenten").EnumerateArray().Select(x => x.GetString()!).ToList();
    }

    [Fact]
    public void Increase_SavesTheMomentInDutchTime()
    {
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("4").ToState("5");

        PublishedMoments().Should().Equal("2026-10-20T10:00:00+02:00");
        _entityManager.Received().SetState(CatVomitTracker.SensorId, 1, Arg.Any<object>());
    }

    [Fact]
    public void Decrease_RemovesTheLatestMoment()
    {
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("4").ToState("5");
        _ctx.AdvanceTimeBy(TimeSpan.FromHours(2).Ticks);
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("5").ToState("6");
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("6").ToState("5");

        PublishedMoments().Should().Equal("2026-10-20T10:00:00+02:00");
    }

    [Fact]
    public void Unavailable_IsNotACountChange()
    {
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("5").ToState("unavailable");
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("unavailable").ToState("5");

        _entityManager.DidNotReceive().SetState(CatVomitTracker.SensorId, 1, Arg.Any<object>());
    }

    [Fact]
    public void OldMoments_AreKeptForever_ButNoLongerCountAsRecent()
    {
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("4").ToState("5");
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(400).Ticks);

        // The nightly update publishes everything again; the state only counts the last two weeks
        PublishedMoments().Should().Equal("2026-10-20T10:00:00+02:00");
        _entityManager.Received().SetState(CatVomitTracker.SensorId, 0, Arg.Any<object>());
    }

    [Fact]
    public void ResettingTheCounter_DoesNotDeleteAnything()
    {
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("4").ToState("5");
        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("5").ToState("6");

        _ctx.ChangeStateFor("counter.pixel_gekotst").FromState("6").ToState("0");

        PublishedMoments().Should().HaveCount(2);
    }
}
