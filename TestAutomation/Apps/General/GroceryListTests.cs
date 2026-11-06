using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class GroceryListTests
{
    private const string VincentId = "person.vincent_maarschalkerweerd";

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void Start(params (string Uid, string Summary)[] open)
    {
        _ctx.HaContext.CallServiceWithResponseAsync("todo", "get_items",
                Arg.Is<ServiceTarget?>(t => t != null && t.EntityIds!.Single() == TodoZoneReminders.GroceryList), Arg.Any<object?>())
            .Returns(Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [TodoZoneReminders.GroceryList] = new { items = open.Select(i => new { uid = i.Uid, summary = i.Summary, status = "needs_action" }) }
            })));
        _ctx.InitApp<GroceryList>(Options.Create(new AppConfig()));
    }

    private void Move(string from, string to)
    {
        _ctx.ChangeStateFor(VincentId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private int TurnedOn() => _ctx.HaContext.ReceivedCalls()
        .Count(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "input_boolean" && (string)c.GetArguments()[1]! == "turn_on");

    [Fact]
    public async Task Pressing_TheButton_TicksOffEveryOpenItem_AndHidesTheButtonOnHome()
    {
        Start(("a1", "Melk"), ("b2", "Brood"));

        _ctx.ChangeStateFor(GroceryList.DoneButton).FromState("unknown").ToState("2026-11-06T10:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        await Task.Delay(100);

        var updates = _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "update_item")
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]))
            .ToList();
        updates.Select(u => u.GetProperty("item").GetString()).Should().BeEquivalentTo("a1", "b2");
        updates.Should().OnlyContain(u => u.GetProperty("status").GetString() == "completed");
        _ctx.VerifyCallService("input_boolean", "turn_off", "boodschappen_afvinken");
    }

    [Fact]
    public async Task AnHourAfterLeavingTheSupermarket_WithOpenItems_TheButtonOnHomeShows()
    {
        Start(("a1", "Melk"));

        Move("Boodschappen AH", "not_home");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(59).Ticks);
        await Task.Delay(50);
        TurnedOn().Should().Be(0);

        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(2).Ticks);
        await Task.Delay(100);
        _ctx.VerifyCallService("input_boolean", "turn_on", "boodschappen_afvinken");
    }

    [Fact]
    public async Task AnHourAfterLeavingTheSupermarket_WithAnEmptyList_NothingShows()
    {
        Start();

        Move("Boodschappen AH", "not_home");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(61).Ticks);
        await Task.Delay(100);

        TurnedOn().Should().Be(0);
    }

    [Fact]
    public async Task GoingBackIntoTheSupermarket_CancelsTheHour()
    {
        Start(("a1", "Melk"));

        Move("Boodschappen AH", "not_home");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(20).Ticks);
        Move("not_home", "Boodschappen AH");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(61).Ticks);
        await Task.Delay(100);

        TurnedOn().Should().Be(0);
    }

    [Fact]
    public async Task LeavingAnotherPlace_StartsNothing()
    {
        Start(("a1", "Melk"));

        Move("Werk", "not_home");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(61).Ticks);
        await Task.Delay(100);

        TurnedOn().Should().Be(0);
    }

    // ---- the WhatsApp message

    [Theory]
    [InlineData("Boodschappen melk", "Melk")]
    [InlineData("boodschappen, ah: melk, brood", "Melk|Brood")]
    [InlineData("AH melk", "Melk")]
    [InlineData("Albert Heijn kaas\nham", "Kaas|Ham")]
    [InlineData("Supermarkt: Pindakaas.", "Pindakaas")]
    [InlineData("  boodschap  Hagelslag ", "Hagelslag")]
    public void ParseMessage_ReadsTheProducts(string text, string expected) =>
        GroceryList.ParseMessage(text).Should().Equal(expected.Split('|'));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Boodschappen")]
    [InlineData("AH, ")]
    [InlineData("doe de lampen aan")]
    [InlineData("Ahoy daar")]
    [InlineData("Ah ik snap het, bedankt dat je het uitlegt aan mij")]
    [InlineData("Boodschappen: wat staat er op de lijst?")]
    public void ParseMessage_IgnoresWhatIsNotForTheList(string? text) =>
        GroceryList.ParseMessage(text).Should().BeNull();
}
