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

public class TodoZoneRemindersTests
{
    private const string VincentId = "person.vincent_maarschalkerweerd";
    private const string VincentWhatsapp = "111@lid";
    private const string CarleenWhatsapp = "222@lid";

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void SetState(string entityId, string state, object? attributes = null) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = attributes == null ? null : JsonSerializer.SerializeToElement(attributes)
        });

    /// <summary>Starts the app with these zones in Home Assistant (the name of a zone is its friendly name).</summary>
    private void Start(params string[] zoneNames)
    {
        var entities = new List<Entity>();
        for (var i = 0; i < zoneNames.Length; i++)
        {
            var id = $"zone.zone{i}";
            SetState(id, "0", new { friendly_name = zoneNames[i] });
            entities.Add(new Entity(_ctx.HaContext, id));
        }

        _ctx.HaContext.GetAllEntities().Returns(entities);
        var config = Options.Create(new AppConfig { Whatsapp = new WhatsAppConfig { Vincent = VincentWhatsapp, Carleen = CarleenWhatsapp } });
        _ctx.InitApp<TodoZoneReminders>(config);
    }

    /// <summary>Makes the list return these open items (summary, description) for <c>todo.get_items</c>.</summary>
    private void OpenItems(string list, params (string Summary, string? Description)[] items) =>
        _ctx.HaContext.CallServiceWithResponseAsync("todo", "get_items",
                Arg.Is<ServiceTarget?>(t => t != null && t.EntityIds!.Single() == list), Arg.Any<object?>())
            .Returns(Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [list] = new
                {
                    items = items.Select(i => i.Description == null
                        ? (object)new { summary = i.Summary, status = "needs_action" }
                        : new { summary = i.Summary, status = "needs_action", description = i.Description })
                }
            })));

    private void Enter(string person, string from, string to)
    {
        _ctx.ChangeStateFor(person).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    private async Task<List<(string To, string Text)>> MessagesAsync()
    {
        await Task.Delay(100);
        return _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "whatsapp")
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]))
            .Select(d => (d.GetProperty("to").GetString()!, d.GetProperty("body").GetProperty("text").GetString()!))
            .ToList();
    }

    // ---- parsing the place of a task

    [Theory]
    [InlineData("📍 Boodschappen AH", "Boodschappen AH", false)]
    [InlineData("📍 Boodschappen AH 🔔", "Boodschappen AH", true)]
    [InlineData("📍 Werk 🔔\nmeer uitleg", "Werk", true)]
    public void ParsePlace_ReadsTheZoneAndTheBell(string description, string zone, bool message) =>
        TodoZoneReminders.ParsePlace(description).Should().Be((zone, message));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gewoon een opmerking")]
    public void ParsePlace_WithoutAPlace_IsNull(string? description) =>
        TodoZoneReminders.ParsePlace(description).Should().BeNull();

    // ---- adding a task from the form

    private void Form(string list, string text, string zone, bool message)
    {
        SetState(TodoZoneReminders.ListHelper, list);
        SetState(TodoZoneReminders.TextHelper, text);
        SetState(TodoZoneReminders.ZoneHelper, zone);
        SetState(TodoZoneReminders.MessageHelper, message ? "on" : "off");
    }

    private void PressAdd()
    {
        _ctx.ChangeStateFor(TodoZoneReminders.AddButton).FromState("2026-10-27T10:00:00").ToState("2026-10-27T10:01:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(1).Ticks);
    }

    private JsonElement AddedItem(string list)
    {
        _ctx.HaContextMock.ProcessPendingOperations();
        return _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "todo" && (string)c.GetArguments()[1]! == "add_item" &&
                        ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() == list)
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]))
            .Single();
    }

    [Fact]
    public void Add_WithAZoneAndTheMessageOn_PutsThePlaceAndTheBellInTheDescription()
    {
        Start("Boodschappen AH");
        Form("Carleen", "Melk halen", "Boodschappen AH", true);

        PressAdd();

        var item = AddedItem("todo.carleen");
        item.GetProperty("item").GetString().Should().Be("Melk halen");
        item.GetProperty("description").GetString().Should().Be("📍 Boodschappen AH 🔔");
    }

    [Fact]
    public void Add_WithAZoneButNoMessage_HasNoBell()
    {
        Start("Boodschappen AH");
        Form("Samen", "Brood", "Boodschappen AH", false);

        PressAdd();

        AddedItem("todo.dagelijks").GetProperty("description").GetString().Should().Be("📍 Boodschappen AH");
    }

    [Fact]
    public void Add_WithoutAZone_HasNoDescription_EvenWithTheMessageOn()
    {
        Start("Boodschappen AH");
        Form("Vincent", "Bellen", TodoZoneReminders.NoZone, true);

        PressAdd();

        AddedItem("todo.vincent").TryGetProperty("description", out _).Should().BeFalse();
    }

    [Fact]
    public void Add_ClearsTheFormExceptTheList()
    {
        Start("Werk");
        Form("Vincent", "Printer", "Werk", true);

        PressAdd();
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.VerifyCallService("input_text", "set_value", "todo_tekst");
        _ctx.VerifyCallService("input_select", "select_option", "todo_zone");
        _ctx.VerifyCallService("input_boolean", "turn_off", "todo_stuur_bericht");
    }

    [Fact]
    public void Add_WithoutAText_DoesNothing()
    {
        Start("Werk");
        Form("Vincent", "  ", "Werk", true);

        PressAdd();
        _ctx.HaContextMock.ProcessPendingOperations();

        _ctx.HaContext.DidNotReceive().CallService("todo", "add_item", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    // ---- the zone choices

    [Fact]
    public void AtStartup_TheZoneChoicesAreSetToNoPlacePlusEveryZone()
    {
        Start("Werk", "Boodschappen COOP ", "Ouders");

        _ctx.HaContext.Received(1).CallService("input_select", "set_options",
            Arg.Is<ServiceTarget?>(t => t!.EntityIds!.Single() == TodoZoneReminders.ZoneHelper),
            Arg.Is<object?>(d => JsonSerializer.SerializeToElement(d).GetProperty("options").EnumerateArray().Select(x => x.GetString())
                .SequenceEqual(new[] { "Geen plek", "Boodschappen COOP", "Ouders", "Werk" })));
    }

    // ---- the reminder

    [Fact]
    public async Task EnteringAZone_SendsTheTasksWithABellForThatZone_FromTheOwnListAndTheSharedList()
    {
        Start("Boodschappen AH", "Werk");
        OpenItems("todo.vincent", ("Melk", "📍 Boodschappen AH 🔔"), ("Printer", "📍 Werk 🔔"), ("Bellen", null));
        OpenItems("todo.dagelijks", ("Brood", "📍 Boodschappen AH 🔔"), ("Koekjes", "📍 Boodschappen AH"));

        Enter(VincentId, "not_home", "Boodschappen AH");

        var messages = await MessagesAsync();
        messages.Should().ContainSingle();
        messages[0].To.Should().Be(VincentWhatsapp);
        messages[0].Text.Should().Be("📍 Je bent bij Boodschappen AH. Dit moet nog:\n• Melk\n• Brood (samen)");
    }

    [Fact]
    public async Task EnteringAZone_WithoutMatchingTasks_SendsNothing()
    {
        Start("Boodschappen AH");
        OpenItems("todo.vincent", ("Printer", "📍 Werk 🔔"), ("Melk", "📍 Boodschappen AH"));
        OpenItems("todo.dagelijks");

        Enter(VincentId, "not_home", "Boodschappen AH");

        (await MessagesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task CarleensTasks_AreNotSentToVincent()
    {
        Start("Boodschappen AH");
        OpenItems("todo.vincent");
        OpenItems("todo.dagelijks");
        OpenItems("todo.carleen", ("Melk", "📍 Boodschappen AH 🔔"));

        Enter(VincentId, "not_home", "Boodschappen AH");

        (await MessagesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ComingHome_UsesTheZoneThuis()
    {
        Start("Werk", "Thuis");
        OpenItems("todo.vincent", ("Pakket uitpakken", "📍 Thuis 🔔"));
        OpenItems("todo.dagelijks");

        Enter(VincentId, "Werk", "home");

        (await MessagesAsync()).Should().ContainSingle().Which.Text.Should().Contain("Je bent bij Thuis").And.Contain("• Pakket uitpakken");
    }

    [Fact]
    public async Task LeavingAZone_SendsNothing()
    {
        Start("Boodschappen AH");
        OpenItems("todo.vincent", ("Melk", "📍 Boodschappen AH 🔔"));
        OpenItems("todo.dagelijks");

        Enter(VincentId, "Boodschappen AH", "not_home");

        (await MessagesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EnteringTheSameZoneAgainWithinHalfAnHour_IsNotSentAgain_ButLaterIs()
    {
        Start("Boodschappen AH");
        OpenItems("todo.vincent", ("Melk", "📍 Boodschappen AH 🔔"));
        OpenItems("todo.dagelijks");

        Enter(VincentId, "not_home", "Boodschappen AH");
        (await MessagesAsync()).Should().HaveCount(1);

        Enter(VincentId, "Boodschappen AH", "not_home");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(10).Ticks);
        Enter(VincentId, "not_home", "Boodschappen AH");
        (await MessagesAsync()).Should().HaveCount(1);

        Enter(VincentId, "Boodschappen AH", "not_home");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(40).Ticks);
        Enter(VincentId, "not_home", "Boodschappen AH");
        (await MessagesAsync()).Should().HaveCount(2);
    }
}
