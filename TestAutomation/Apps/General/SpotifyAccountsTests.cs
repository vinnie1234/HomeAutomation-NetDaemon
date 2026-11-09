using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class SpotifyAccountsTests
{
    private const string VincentEntry = "01ENTRYVINCENT";
    private const string CarleenEntry = "01ENTRYCARLEEN";

    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly List<Entity> _entities = [];

    private void SetState(string entityId, string state, object? attributes = null) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = attributes == null ? null : JsonSerializer.SerializeToElement(attributes)
        });

    /// <summary>A Spotcast account is known by the profile sensor it has.</summary>
    private void AddAccount(string userId, string entryId, string name)
    {
        var id = $"sensor.spotcast_{userId}_spotify_profile";
        SetState(id, "ok", new { entry_id = entryId, display_name = name });
        _entities.Add(new Entity(_ctx.HaContext, id));
        _ctx.HaContext.GetAllEntities().Returns(_entities.ToList());
    }

    private void Start(string vincentChoice = SpotifyChoice.NoAccount, string[]? currentOptions = null)
    {
        foreach (var person in SpotifyChoice.People)
            SetState(SpotifyChoice.AccountHelper(person), person == "vincent" ? vincentChoice : SpotifyChoice.NoAccount,
                new { options = currentOptions ?? [SpotifyChoice.NoAccount] });
        _ctx.HaContext.GetAllEntities().Returns(_entities.ToList());
        _ctx.InitApp<SpotifyAccounts>(Options.Create(new AppConfig()));
    }

    private bool OptionsSet(string person, params string[] options) => _ctx.HaContext.ReceivedCalls().Any(c =>
        c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "set_options" &&
        ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() == SpotifyChoice.AccountHelper(person) &&
        JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("options").EnumerateArray().Select(x => x.GetString()).SequenceEqual(options));

    private List<string> Selected(string person) => _ctx.HaContext.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "select_option" &&
                    ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() == SpotifyChoice.AccountHelper(person))
        .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("option").GetString()!).ToList();

    [Fact]
    public void AtStartup_TheChoicesAreTheDefaultAccountPlusEveryAccountSpotcastHas()
    {
        AddAccount("111", VincentEntry, "Vincent");
        AddAccount("222", CarleenEntry, "Carleen");

        Start();

        OptionsSet("vincent", SpotifyChoice.NoAccount, "Carleen", "Vincent").Should().BeTrue();
        OptionsSet("carleen", SpotifyChoice.NoAccount, "Carleen", "Vincent").Should().BeTrue();
    }

    [Fact]
    public void AChosenAccountThatWasRemoved_GoesBackToTheDefaultAccount()
    {
        AddAccount("111", VincentEntry, "Vincent");

        Start(vincentChoice: "Oud account", currentOptions: [SpotifyChoice.NoAccount, "Oud account", "Vincent"]);

        Selected("vincent").Should().Equal(SpotifyChoice.NoAccount);
    }

    [Fact]
    public void LinkingANewAccount_ChoosesItForThePersonWhoPressedTheButton()
    {
        AddAccount("111", VincentEntry, "Vincent");
        Start();

        _ctx.ChangeStateFor(SpotifyAccounts.LinkButtonPrefix + "carleen").FromState("unknown").ToState("2026-11-09T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(30).Ticks);
        Selected("carleen").Should().BeEmpty();

        AddAccount("222", CarleenEntry, "Carleen");
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(30).Ticks);

        Selected("carleen").Should().Equal("Carleen");
        Selected("vincent").Should().BeEmpty();
        OptionsSet("carleen", SpotifyChoice.NoAccount, "Carleen", "Vincent").Should().BeTrue();
    }

    [Fact]
    public void WithoutANewAccount_NothingIsChosen_AndItStopsLookingAfterTenMinutes()
    {
        AddAccount("111", VincentEntry, "Vincent");
        Start();

        _ctx.ChangeStateFor(SpotifyAccounts.LinkButtonPrefix + "carleen").FromState("unknown").ToState("2026-11-09T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(11).Ticks);
        AddAccount("222", CarleenEntry, "Carleen");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);

        Selected("carleen").Should().BeEmpty();
    }
}
