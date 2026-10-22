using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Models.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetDaemon.Client;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HelperManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IHomeAssistantRunner _runner = Substitute.For<IHomeAssistantRunner>();
    private readonly IHomeAssistantConnection _connection = Substitute.For<IHomeAssistantConnection>();
    private readonly HelperManager _app;

    private static readonly HelperDefinition WorkingVincent =
        new("input_boolean", "workingvincent", "WorkingVincent", new Dictionary<string, object> { ["icon"] = "mdi:briefcase" });

    private static readonly HelperDefinition Peuken =
        new("input_number", "peuken_pakjes", "Peuken Pakjes", new Dictionary<string, object> { ["min"] = 0d, ["max"] = 1000d, ["mode"] = "box" });

    public HelperManagerTests()
    {
        // No connection while the app starts, so it doesn't check the real list; the tests call EnsureHelpersAsync themselves
        _runner.CurrentConnection.Returns((IHomeAssistantConnection?)null);
        _app = _ctx.InitApp<HelperManager>(Options.Create(new AppConfig()), _runner);
        _runner.CurrentConnection.Returns(_connection);
    }

    private void Existing(string domain, params string[] ids) =>
        _connection.SendCommandAndReturnResponseAsync<ListHelpersCommand, IReadOnlyList<HelperItem>>(
                Arg.Is<ListHelpersCommand>(c => c.Type == $"{domain}/list"), Arg.Any<CancellationToken>())
            .Returns(ids.Select(id => new HelperItem { Id = id, Name = id }).ToList());

    private List<CreateHelperCommand> CreateCommands() =>
        _connection.ReceivedCalls()
            .Select(c => c.GetArguments()[0])
            .OfType<CreateHelperCommand>()
            .ToList();

    [Fact]
    public async Task MissingHelper_IsCreatedWithItsSettings()
    {
        Existing("input_boolean");
        Existing("input_number", "peuken_pakjes");

        var created = await _app.EnsureHelpersAsync([WorkingVincent, Peuken]);

        created.Should().Equal(WorkingVincent);
        var json = JsonSerializer.SerializeToElement(CreateCommands().Single());
        json.GetProperty("type").GetString().Should().Be("input_boolean/create");
        json.GetProperty("name").GetString().Should().Be("WorkingVincent");
        json.GetProperty("icon").GetString().Should().Be("mdi:briefcase");
    }

    [Fact]
    public async Task NumberSettings_AreSentAsFields()
    {
        Existing("input_number");

        await _app.EnsureHelpersAsync([Peuken]);

        var json = JsonSerializer.SerializeToElement(CreateCommands().Single());
        json.GetProperty("type").GetString().Should().Be("input_number/create");
        json.GetProperty("min").GetDouble().Should().Be(0);
        json.GetProperty("max").GetDouble().Should().Be(1000);
        json.GetProperty("mode").GetString().Should().Be("box");
    }

    [Fact]
    public async Task ExistingHelpers_AreLeftAlone()
    {
        Existing("input_boolean", "workingvincent");
        Existing("input_number", "peuken_pakjes");

        var created = await _app.EnsureHelpersAsync([WorkingVincent, Peuken]);

        created.Should().BeEmpty();
        CreateCommands().Should().BeEmpty();
    }

    [Fact]
    public async Task DomainThatCannotBeListed_IsSkipped()
    {
        _connection.SendCommandAndReturnResponseAsync<ListHelpersCommand, IReadOnlyList<HelperItem>>(
                Arg.Any<ListHelpersCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no connection"));

        var created = await _app.EnsureHelpersAsync([WorkingVincent]);

        created.Should().BeEmpty();
        CreateCommands().Should().BeEmpty();
    }

    private static readonly HelperDefinition Theme =
        new("input_select", "weerkaart_thema", "weerkaart_thema", new Dictionary<string, object> { ["options"] = new[] { "Automatisch", "Kerst", "Halloween" } });

    [Fact]
    public async Task ExistingSelect_GetsTheMissingOptionsAddedAtTheEnd()
    {
        _connection.SendCommandAndReturnResponseAsync<ListHelpersCommand, IReadOnlyList<HelperItem>>(Arg.Any<ListHelpersCommand>(), Arg.Any<CancellationToken>())
            .Returns(new List<HelperItem> { new() { Id = "weerkaart_thema", Name = "weerkaart_thema", Options = ["Kerst", "Automatisch", "Eigen"] } });

        await _app.EnsureHelpersAsync([Theme]);

        var update = _connection.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<UpdateHelperCommand>().Single();
        var json = JsonSerializer.SerializeToElement(update);
        json.GetProperty("type").GetString().Should().Be("input_select/update");
        json.GetProperty("input_select_id").GetString().Should().Be("weerkaart_thema");
        json.GetProperty("options").EnumerateArray().Select(x => x.GetString()).Should().Equal("Kerst", "Automatisch", "Eigen", "Halloween");
        CreateCommands().Should().BeEmpty();
    }

    [Fact]
    public async Task ExistingSelect_WithAllOptions_IsNotUpdated()
    {
        _connection.SendCommandAndReturnResponseAsync<ListHelpersCommand, IReadOnlyList<HelperItem>>(Arg.Any<ListHelpersCommand>(), Arg.Any<CancellationToken>())
            .Returns(new List<HelperItem> { new() { Id = "weerkaart_thema", Name = "weerkaart_thema", Options = ["Automatisch", "Kerst", "Halloween"] } });

        await _app.EnsureHelpersAsync([Theme]);

        _connection.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<UpdateHelperCommand>().Should().BeEmpty();
    }

    [Fact]
    public void Definitions_HaveUniqueIdsPerDomain()
    {
        HelperDefinitions.All.GroupBy(x => (x.Domain, x.Id)).Where(g => g.Count() > 1).Select(g => g.Key)
            .Should().BeEmpty();
    }

    [Fact]
    public void Definitions_ContainTheHelpersTheAppsUse()
    {
        HelperDefinitions.All.Select(x => $"{x.Domain}.{x.Id}").Should().Contain(
        [
            "input_boolean.working", "input_boolean.workingvincent", "input_boolean.workingcarleen",
            "input_number.peuken_pakjes", "counter.pixel_gekotst", "input_select.weerkaart_thema"
        ]);
    }

    [Theory]
    [InlineData("on", "Kantoor")]
    [InlineData("off", "Thuis")]
    public async Task NewHelper_TakesOverTheValueOfTheHelperItReplaces(string officeDay, string expected)
    {
        Existing("input_select");
        _connection.SendCommandAndReturnResponseAsync<CreateHelperCommand, HelperItem>(Arg.Any<CreateHelperCommand>(), Arg.Any<CancellationToken>())
            .Returns(new HelperItem { Id = "werkdag_vincent_maandag", Name = "Werkdag Vincent maandag" });
        _ctx.HaContext.GetState("input_boolean.officeday_monday")
            .Returns(new NetDaemon.HassModel.Entities.EntityState { EntityId = "input_boolean.officeday_monday", State = officeDay });
        var helper = HelperDefinitions.All.Single(x => x.Id == "werkdag_vincent_maandag");

        await _app.EnsureHelpersAsync([helper]);

        _ctx.HaContext.Received(1).CallService("input_select", "select_option",
            Arg.Is<NetDaemon.HassModel.Entities.ServiceTarget>(t => t.EntityIds!.Single() == "input_select.werkdag_vincent_maandag"),
            Arg.Is<object>(d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).Contains(expected)));
    }

    [Fact]
    public void Definitions_GiveEveryoneTheSameWorkSchedule()
    {
        foreach (var person in new[] { "vincent", "carleen" })
            HelperDefinitions.All.Select(x => x.Id).Should().Contain(
            [
                $"werkdag_{person}_maandag", $"werkdag_{person}_vrijdag", $"vrij_{person}",
                $"opstaan_kantoor_{person}", $"opstaan_thuiswerk_{person}", $"werk_start_{person}", $"werk_eind_{person}"
            ]);
    }
}
