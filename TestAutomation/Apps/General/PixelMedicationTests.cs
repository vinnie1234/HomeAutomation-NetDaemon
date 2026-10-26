using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.Pixel;
using Automation.Repository;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class PixelMedicationTests : IDisposable
{
    private const string PrednisonStock = "input_number.pixel_prednison_voorraad";
    private const string AntibraakStock = "input_number.pixel_antibraak_voorraad";
    private const string PrednisonButton = "input_button.pixel_prednison_gegeven";
    private const string AntibraakButton = "input_button.pixel_antibraak_gegeven";

    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"PixelMedicationTests_{Guid.NewGuid():N}");
    private readonly IDataRepository _storage;
    private PixelMedication _app = null!;

    public PixelMedicationTests()
    {
        Directory.CreateDirectory(_storagePath);
        _storage = new DataRepository(_storagePath, Substitute.For<ILogger>());
        _entityManager.EntityExists(PixelMedication.SensorId).Returns(true);
        SetState(PrednisonStock, "10");
        SetState(AntibraakStock, "8");
        SetState("input_datetime.pixel_prednison_tijd", "20:00:00");
        SetState("input_number.pixel_prednison_doosprijs", "7.85");
        SetState("input_number.pixel_prednison_doosgrootte", "21");
        SetState("input_number.pixel_antibraak_doosprijs", "20.30");
        SetState("input_number.pixel_antibraak_doosgrootte", "4");
    }

    public void Dispose() => Directory.Delete(_storagePath, recursive: true);

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    /// <summary>Starts the app at the given Dutch time (October: UTC+2).</summary>
    private void StartAt(int day, int hour, int minute = 0)
    {
        _ctx.SetCurrentTime(new DateTime(2026, 10, day, hour - 2, minute, 0, DateTimeKind.Utc));
        _app = _ctx.InitApp<PixelMedication>(Options.Create(new AppConfig()), _storage, _entityManager);
    }

    private void At(int day, int hour, int minute = 0) =>
        _ctx.AdvanceTimeTo(new DateTimeOffset(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(2)).UtcTicks);

    private void Press(string button) =>
        _ctx.ChangeStateFor(button).FromState("2026-10-01T00:00:00+00:00").ToState(Guid.NewGuid().ToString());

    /// <summary>The value the app set the stock helper to.</summary>
    private double SetStock(string entityId) =>
        _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "set_value"
                        && ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() == entityId)
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("value").GetDouble())
            .Single();

    private List<MedicationEvent> History() => _storage.Get<List<MedicationEvent>>("PixelMedicationHistory") ?? [];

    [Fact]
    public void GivingPrednison_TakesAQuarterPillOffTheStock_AndSavesIt()
    {
        StartAt(20, 20, 30);

        Press(PrednisonButton);

        SetStock(PrednisonStock).Should().Be(9.75);
        var dose = History().Should().ContainSingle().Subject;
        dose.Medicine.Should().Be(PixelMedication.Prednison);
        dose.Kind.Should().Be("gegeven");
        dose.Amount.Should().Be(-0.25);
        dose.Stock.Should().Be(9.75);
    }

    [Fact]
    public void GivingAntibraak_TakesHalfAPillOffTheStock()
    {
        StartAt(20, 9);

        Press(AntibraakButton);

        SetStock(AntibraakStock).Should().Be(7.5);
        History().Should().ContainSingle().Which.Medicine.Should().Be(PixelMedication.Antibraak);
    }

    [Fact]
    public void Stock_NeverGoesBelowZero()
    {
        SetState(PrednisonStock, "0");
        StartAt(20, 20, 30);

        Press(PrednisonButton);

        SetStock(PrednisonStock).Should().Be(0);
    }

    [Fact]
    public void ChangingTheStockByHand_IsSavedAsOneStockChange_OnceItSettles()
    {
        StartAt(20, 10);

        // Clicking the arrow a few times: one line with the total, once the field is quiet
        _ctx.ChangeStateFor(AntibraakStock).FromState("8").ToState("8.5");
        _ctx.ChangeStateFor(AntibraakStock).FromState("8.5").ToState("9");
        _ctx.ChangeStateFor(AntibraakStock).FromState("9").ToState("38");
        History().Should().BeEmpty();
        At(20, 10, 1);

        var change = History().Should().ContainSingle().Subject;
        change.Kind.Should().Be("voorraad");
        change.Amount.Should().Be(30);
        change.Stock.Should().Be(38);
    }

    [Fact]
    public void TheAppsOwnStockChange_IsNotSavedTwice()
    {
        StartAt(20, 20, 30);
        Press(PrednisonButton);

        _ctx.ChangeStateFor(PrednisonStock).FromState("10").ToState("9.75");

        History().Should().ContainSingle().Which.Kind.Should().Be("gegeven");
    }

    [Fact]
    public void Status_IsNeeded_AfterTheReminderTime_UntilGiven()
    {
        StartAt(20, 13);

        At(20, 19, 59);
        _app.PrednisonStatus().Should().Be("wacht");
        At(20, 20);
        _app.PrednisonStatus().Should().Be("nodig");

        Press(PrednisonButton);
        _app.PrednisonStatus().Should().Be("gegeven");
    }

    [Fact]
    public void Sensor_ShowsNeeded_AtTheReminderTime()
    {
        StartAt(20, 13);

        // Half a minute after the reminder time, so a timer that fires a little early still sees the reminder time passed
        At(20, 20, 1);

        var call = _entityManager.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IEntityManager.SetState));
        call.GetArguments()[1].Should().Be("nodig");
    }

    [Fact]
    public void Status_IsForgotten_TheNextDay_UntilTheNextDose()
    {
        StartAt(20, 11);

        // The evening of the 20th passes without a dose
        At(21, 0, 30);
        _app.PrednisonStatus().Should().Be("vergeten");
        At(21, 15);
        _app.PrednisonStatus().Should().Be("vergeten");

        Press(PrednisonButton);
        _app.PrednisonStatus().Should().Be("gegeven");
    }

    [Fact]
    public void TheDayChangesAtMidnight()
    {
        StartAt(20, 11);
        At(20, 21);
        Press(PrednisonButton);
        _app.PrednisonStatus().Should().Be("gegeven");

        // The next morning the dose of last evening no longer counts for today
        At(21, 9);
        _app.PrednisonStatus().Should().Be("wacht");
        At(21, 20, 30);
        _app.PrednisonStatus().Should().Be("nodig");
    }

    [Fact]
    public void NoForgottenWarning_ForTheDaysBeforeTracking()
    {
        StartAt(20, 15);

        At(20, 16);

        _app.PrednisonStatus().Should().Be("wacht");
    }

    [Fact]
    public void Publish_ShowsStockLowStockAndHistory()
    {
        SetState(PrednisonStock, "1.5");
        SetState(AntibraakStock, "1");
        StartAt(20, 20, 30);
        Press(PrednisonButton);

        var call = _entityManager.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IEntityManager.SetState));
        call.GetArguments()[1].Should().Be("gegeven");
        var attributes = JsonSerializer.SerializeToElement(call.GetArguments()[2]);
        attributes.GetProperty("prednison_bijna_op").GetBoolean().Should().BeTrue();
        attributes.GetProperty("antibraak_bijna_op").GetBoolean().Should().BeTrue();
        attributes.GetProperty("geschiedenis")[0].GetProperty("medicijn").GetString().Should().Be("prednison");
    }

    private JsonElement PublishedAttributes() =>
        JsonSerializer.SerializeToElement(_entityManager.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IEntityManager.SetState)).GetArguments()[2]);

    [Fact]
    public void NewBox_IsSavedWithItsCost_AndCountsInTheTotal()
    {
        SetState(AntibraakStock, "0");
        SetState(PrednisonStock, "0");
        StartAt(20, 10);

        _ctx.ChangeStateFor(AntibraakStock).FromState("0").ToState("4");
        _ctx.ChangeStateFor(PrednisonStock).FromState("0").ToState("21");
        SetState(AntibraakStock, "4");
        SetState(PrednisonStock, "21");
        At(20, 10, 1);

        History().Should().OnlyContain(x => x.Cost != null);
        History().Single(x => x.Medicine == PixelMedication.Antibraak).Cost.Should().Be(20.30);
        var attributes = PublishedAttributes();
        attributes.GetProperty("antibraak_prijs_per_pil").GetDouble().Should().Be(5.075);
        attributes.GetProperty("prednison_kosten").GetDouble().Should().Be(7.85);
        attributes.GetProperty("kosten_totaal").GetDouble().Should().Be(28.15);
    }

    [Fact]
    public void OldStockLines_WithoutAPrice_CountAtTheCurrentPrice()
    {
        _storage.Save("PixelMedicationHistory", new List<MedicationEvent>
        {
            new() { Time = DateTimeOffset.Parse("2026-10-19T16:30:00+02:00"), Medicine = PixelMedication.Prednison, Kind = "voorraad", Amount = 21, Stock = 21 }
        });
        StartAt(20, 10);

        PublishedAttributes().GetProperty("prednison_kosten").GetDouble().Should().Be(7.85);
    }

    private int PhoneNotifications(string titlePart) =>
        _ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "mobile_app_vincent_phone"
            && JsonSerializer.Serialize(c.GetArguments()[3]).Contains(titlePart));

    [Fact]
    public void PhoneReminder_AnHourAfterTheReminder_WhenStillNeeded()
    {
        StartAt(20, 13);

        At(20, 21, 1);

        PhoneNotifications("prednison nog niet gehad").Should().Be(1);
        _ctx.VerifyCallNotify("notify", "mobile_app_carleen_mobiel");
    }

    [Fact]
    public void NoPhoneReminder_WhenAlreadyGiven()
    {
        StartAt(20, 13);
        At(20, 20, 30);
        Press(PrednisonButton);

        At(20, 21, 1);

        PhoneNotifications("prednison nog niet gehad").Should().Be(0);
    }

    [Fact]
    public void MorningNotification_WhenForgotten()
    {
        StartAt(20, 11);

        At(21, 8, 1);

        PhoneNotifications("Prednison vergeten").Should().Be(1);
    }

    [Fact]
    public void Undo_PutsThePillBack_KeepsTheDoseAndAddsACorrection()
    {
        StartAt(20, 20, 30);
        Press(PrednisonButton);
        SetState(PrednisonStock, "9.75");

        Press("input_button.pixel_medicijn_ongedaan");

        var history = History();
        history.Should().HaveCount(2);
        history[0].Kind.Should().Be("gegeven");
        history[1].Kind.Should().Be("correctie");
        history[1].Corrects.Should().Be(history[0].Time);
        history[1].Stock.Should().Be(10);
        _app.PrednisonStatus().Should().Be("nodig");
    }

    [Fact]
    public void Undo_TwiceInARow_UndoesTheDoseBeforeIt()
    {
        StartAt(20, 9);
        Press(AntibraakButton);
        At(20, 20, 30);
        Press(PrednisonButton);

        Press("input_button.pixel_medicijn_ongedaan");
        Press("input_button.pixel_medicijn_ongedaan");

        History().Where(x => x.Kind == "correctie").Select(x => x.Medicine)
            .Should().Equal(PixelMedication.Prednison, PixelMedication.Antibraak);
    }
}
