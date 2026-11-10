using System.Text.Json;
using Automation.apps.General.Assistants;
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

    private void AddEntity(string id, string state, object attributes)
    {
        SetState(id, state, attributes);
        _entities.Add(new Entity(_ctx.HaContext, id));
        _ctx.HaContext.GetAllEntities().Returns([.. _entities]);
    }

    /// <summary>A Spotcast account is known by the profile sensor it has.</summary>
    private void AddSpotcast(string userId, string entryId, string name) =>
        AddEntity($"sensor.spotcast_{userId}_spotify_profile", "ok", new { entry_id = entryId, display_name = name });

    private void AddSpotify(string name) =>
        AddEntity($"media_player.spotify_{name.ToLowerInvariant()}", "idle", new { friendly_name = $"Spotify {name}" });

    private void AddSpotifyPlus(string name) =>
        AddEntity($"media_player.spotifyplus_{name.ToLowerInvariant()}", "off", new { friendly_name = $"SpotifyPlus {name}" });

    private void Start(string vincentChoice = SpotifyChoice.NoAccount, string[]? currentOptions = null, AppConfig? config = null)
    {
        foreach (var person in SpotifyChoice.People)
            SetState(SpotifyChoice.AccountHelper(person), person == "vincent" ? vincentChoice : SpotifyChoice.NoAccount,
                new { options = currentOptions ?? [SpotifyChoice.NoAccount] });
        _ctx.HaContext.GetAllEntities().Returns([.. _entities]);
        _ctx.InitApp<SpotifyAccounts>(Options.Create(config ?? new AppConfig()));
    }

    private bool OptionsSet(string person, params string[] options) => _ctx.HaContext.ReceivedCalls().Any(c =>
        c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "set_options" &&
        ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() == SpotifyChoice.AccountHelper(person) &&
        JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("options").EnumerateArray().Select(x => x.GetString()).SequenceEqual(options));

    private List<string> Selected(string person) =>
    [
        .. _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[1]! == "select_option" &&
                        ((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single() ==
                        SpotifyChoice.AccountHelper(person))
            .Select(c => JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("option").GetString()!)
    ];

    private List<(string Helper, string Value)> Filled() =>
    [
        .. _ctx.HaContext.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "CallService" && (string)c.GetArguments()[0]! == "input_text" &&
                        (string)c.GetArguments()[1]! == "set_value")
            .Select(c => (((ServiceTarget)c.GetArguments()[2]!).EntityIds!.Single(),
                JsonSerializer.SerializeToElement(c.GetArguments()[3]).GetProperty("value").GetString()!))
    ];

    [Fact]
    public void AtStartup_TheChoicesAreTheDefaultAccountPlusEveryAccountOfTheThreeIntegrations_WithWhereItIsLinked()
    {
        AddSpotcast("111", VincentEntry, "Vincent");
        AddSpotify("Vincent");
        AddSpotifyPlus("Vincent");
        AddSpotify("Carleen");

        Start();

        OptionsSet("vincent", SpotifyChoice.NoAccount, "Carleen (Spotify)", "Vincent (Spotcast, Spotify, SpotifyPlus)").Should().BeTrue();
        OptionsSet("carleen", SpotifyChoice.NoAccount, "Carleen (Spotify)", "Vincent (Spotcast, Spotify, SpotifyPlus)").Should().BeTrue();
    }

    [Fact]
    public void AChosenAccountThatWasRemoved_GoesBackToTheDefaultAccount()
    {
        AddSpotcast("111", VincentEntry, "Vincent");

        Start(vincentChoice: "Oud account (Spotcast)", currentOptions: [SpotifyChoice.NoAccount, "Oud account (Spotcast)", "Vincent (Spotcast)"]);

        Selected("vincent").Should().Equal(SpotifyChoice.NoAccount);
    }

    [Fact]
    public void AnAccountThatGotLinkedInOneMoreIntegration_StaysChosenUnderItsNewName()
    {
        AddSpotcast("111", VincentEntry, "Vincent");
        AddSpotify("Vincent");

        Start(vincentChoice: "Vincent (Spotcast)", currentOptions: [SpotifyChoice.NoAccount, "Vincent (Spotcast)"]);

        Selected("vincent").Should().Equal("Vincent (Spotcast, Spotify)");
    }

    [Fact]
    public void LinkingANewAccount_ChoosesItForThePersonWhoPressedTheButton()
    {
        AddSpotcast("111", VincentEntry, "Vincent");
        Start();

        _ctx.ChangeStateFor(SpotifyAccounts.LinkButtonPrefix + "carleen").FromState("unknown").ToState("2026-11-09T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(30).Ticks);
        Selected("carleen").Should().BeEmpty();

        AddSpotcast("222", CarleenEntry, "Carleen");
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(30).Ticks);

        Selected("carleen").Should().Equal("Carleen (Spotcast)");
        Selected("vincent").Should().BeEmpty();
    }

    [Fact]
    public void LinkingAnAccountInOneMoreIntegration_IsPickedUpToo()
    {
        AddSpotify("Carleen");
        Start();

        _ctx.ChangeStateFor(SpotifyAccounts.LinkButtonPrefix + "carleen").FromState("unknown").ToState("2026-11-09T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        AddSpotcast("222", CarleenEntry, "Carleen");
        _ctx.AdvanceTimeBy(TimeSpan.FromSeconds(30).Ticks);

        Selected("carleen").Should().Equal("Carleen (Spotcast, Spotify)");
    }

    [Fact]
    public void WithoutANewAccount_NothingIsChosen_AndItStopsLookingAfterTenMinutes()
    {
        AddSpotcast("111", VincentEntry, "Vincent");
        Start();

        _ctx.ChangeStateFor(SpotifyAccounts.LinkButtonPrefix + "carleen").FromState("unknown").ToState("2026-11-09T12:00:00+00:00");
        _ctx.HaContextMock.ProcessPendingOperations();
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(11).Ticks);
        AddSpotcast("222", CarleenEntry, "Carleen");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);

        Selected("carleen").Should().BeEmpty();
    }

    // ---- playlists that were never filled in show what config.json has

    private static AppConfig WithPlaylists() => new()
    {
        SpotifyDoucheUrl = "config:douche",
        SpotifyKeukenUrl = "config:keuken",
        SpotifyRadioNlUrl = "config:radio",
        SpotifyKerstUrl = "config:kerst"
    };

    [Fact]
    public void AnUnknownPlaylist_IsFilledWithTheOneInConfigJson_ForBothPersons()
    {
        foreach (var person in SpotifyChoice.People)
            foreach (var room in Enum.GetValues<SpotifyRoom>())
                SetState(SpotifyChoice.PlaylistHelper(room, person), "unknown");

        Start(config: WithPlaylists());

        Filled().Should().BeEquivalentTo(
        [
            ("input_text.spotify_douche_vincent", "config:douche"), ("input_text.spotify_keuken_vincent", "config:keuken"),
            ("input_text.spotify_radio_vincent", "config:radio"), ("input_text.spotify_kerst_vincent", "config:kerst"),
            ("input_text.spotify_douche_carleen", "config:douche"), ("input_text.spotify_keuken_carleen", "config:keuken"),
            ("input_text.spotify_radio_carleen", "config:radio"), ("input_text.spotify_kerst_carleen", "config:kerst")
        ]);
    }

    [Fact]
    public void APlaylistThatIsFilledIn_IsLeftAlone_AndAHelperThatDoesNotExistYetIsSkipped()
    {
        SetState("input_text.spotify_douche_vincent", "https://open.spotify.com/playlist/mine");

        Start(config: WithPlaylists());

        Filled().Should().BeEmpty();
    }
}
