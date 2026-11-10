using System.Text.Json;
using Automation.Configuration;
using Automation.Helpers;
using FluentAssertions;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using Xunit;

namespace TestAutomation.Helpers;

public class SpotifyChoiceTests
{
    private readonly IHaContext _ha = Substitute.For<IHaContext>();
    private readonly List<Entity> _entities = [];

    private readonly AppConfig _config = new()
    {
        SpotifyDoucheUrl = "config:douche",
        SpotifyRadioNlUrl = "config:radio",
        SpotifyKeukenUrl = "config:keuken",
        SpotifyKerstUrl = "config:kerst"
    };

    public SpotifyChoiceTests() => _ha.GetAllEntities().Returns(_ => [.. _entities]);

    private void SetState(string entityId, string state, object? attributes = null) =>
        _ha.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = attributes == null ? null : JsonSerializer.SerializeToElement(attributes)
        });

    private void AddAccount(string userId, string entryId, string name)
    {
        var id = $"sensor.spotcast_{userId}_spotify_profile";
        SetState(id, "ok", new { entry_id = entryId, display_name = name });
        _entities.Add(new Entity(_ha, id));
    }

    [Theory]
    [InlineData(SpotifyRoom.Douche, true, "config:douche")]
    [InlineData(SpotifyRoom.Douche, false, "config:radio")]
    [InlineData(SpotifyRoom.Keuken, false, "config:keuken")]
    [InlineData(SpotifyRoom.Radio, true, "config:radio")]
    [InlineData(SpotifyRoom.Kerst, true, "config:kerst")]
    public void WithNothingSet_ThePlaylistIsWhatConfigJsonHas_AndThereIsNoAccount(SpotifyRoom room, bool carleenHome, string expected)
    {
        var (url, account) = SpotifyChoice.Resolve(_ha, _config, room, vincentHome: true, carleenHome);

        url.Should().Be(expected);
        account.Should().BeNull();
    }

    [Fact]
    public void ThePersonWhoIsHomeAlone_DecidesThePlaylist()
    {
        SetState("input_text.spotify_keuken_vincent", "vincent:keuken");
        SetState("input_text.spotify_keuken_carleen", "carleen:keuken");

        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, vincentHome: false, carleenHome: true).Url.Should().Be("carleen:keuken");
        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, vincentHome: true, carleenHome: false).Url.Should().Be("vincent:keuken");
    }

    [Fact]
    public void WhenBothAreHome_TheDefaultPersonDecides_AndVincentIsTheDefault()
    {
        SetState("input_text.spotify_keuken_vincent", "vincent:keuken");
        SetState("input_text.spotify_keuken_carleen", "carleen:keuken");

        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, true, true).Url.Should().Be("vincent:keuken");

        SetState(SpotifyChoice.DefaultPersonHelper, "Carleen");
        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, true, true).Url.Should().Be("carleen:keuken");
    }

    [Fact]
    public void APlaylistThatIsEmptyOrUnknown_FallsBackToConfigJson()
    {
        SetState("input_text.spotify_kerst_vincent", "  ");
        SetState("input_text.spotify_keuken_vincent", "unknown");

        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Kerst, true, false).Url.Should().Be("config:kerst");
        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, true, false).Url.Should().Be("config:keuken");
    }

    private void AddPlayer(string id, string friendlyName)
    {
        SetState(id, "idle", new { friendly_name = friendlyName });
        _entities.Add(new Entity(_ha, id));
    }

    [Fact]
    public void Accounts_AreMergedByNameFromSpotcastSpotifyAndSpotifyPlus()
    {
        AddAccount("111", "01ENTRYVINCENT", "Vincent M");
        AddPlayer("media_player.spotify_vincent_m", "Spotify Vincent M");
        AddPlayer("media_player.spotifyplus_vincent_m", "SpotifyPlus Vincent M");
        AddPlayer("media_player.spotify_carleen_m", "Spotify Carleen M");

        var accounts = SpotifyChoice.Accounts(_ha);

        accounts.Select(x => x.Label).Should().Equal("Carleen M (Spotify)", "Vincent M (Spotcast, Spotify, SpotifyPlus)");
        accounts.Single(x => x.Name == "Vincent M").SpotcastEntryId.Should().Be("01ENTRYVINCENT");
        accounts.Single(x => x.Name == "Carleen M").InSpotcast.Should().BeFalse();
    }

    [Fact]
    public void AnAccountThatIsNotLinkedInSpotcast_PlaysOnTheDefaultAccount()
    {
        AddPlayer("media_player.spotify_carleen_m", "Spotify Carleen M");
        SetState(SpotifyChoice.AccountHelper("carleen"), "Carleen M (Spotify)");

        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, false, true).Account.Should().BeNull();
    }

    [Theory]
    [InlineData("Vincent M (Spotcast, Spotify)", "Vincent M")]
    [InlineData("Vincent M", "Vincent M")]
    [InlineData("  Vincent M (Spotcast)  ", "Vincent M")]
    public void TheNameOfAChoice_LeavesOutTheIntegrations(string label, string expected) =>
        SpotifyChoice.NameOf(label).Should().Be(expected);

    [Fact]
    public void TheChosenAccount_BecomesTheSpotcastEntryOfThatAccount()
    {
        AddAccount("111", "01ENTRYVINCENT", "Vincent M");
        AddAccount("222", "01ENTRYCARLEEN", "Carleen M");
        AddPlayer("media_player.spotify_carleen_m", "Spotify Carleen M");
        SetState(SpotifyChoice.AccountHelper("carleen"), "carleen m (Spotcast, Spotify)");

        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, false, true).Account.Should().Be("01ENTRYCARLEEN");
        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, true, false).Account.Should().BeNull();
    }

    [Theory]
    [InlineData(SpotifyChoice.NoAccount)]
    [InlineData("Een account dat weg is")]
    public void NoAccountOrOneThatIsGone_MeansTheDefaultAccount(string chosen)
    {
        AddAccount("111", "01ENTRYVINCENT", "Vincent M");
        SetState(SpotifyChoice.AccountHelper("vincent"), chosen);

        SpotifyChoice.Resolve(_ha, _config, SpotifyRoom.Keuken, true, false).Account.Should().BeNull();
    }

    [Fact]
    public void Autoplay_IsOnUntilTheSwitchOfThePersonWhoseSettingsCountIsOff()
    {
        SpotifyChoice.Autoplay(_ha, SpotifyRoom.Keuken, true, false).Should().BeTrue();

        SetState(SpotifyChoice.AutoplayHelper(SpotifyRoom.Keuken, "vincent"), "off");
        SetState(SpotifyChoice.AutoplayHelper(SpotifyRoom.Keuken, "carleen"), "on");

        SpotifyChoice.Autoplay(_ha, SpotifyRoom.Keuken, true, false).Should().BeFalse("only Vincent is home");
        SpotifyChoice.Autoplay(_ha, SpotifyRoom.Keuken, false, true).Should().BeTrue("only Carleen is home");
        SpotifyChoice.Autoplay(_ha, SpotifyRoom.Douche, true, false).Should().BeTrue("another room has its own switch");
    }

    [Fact]
    public void Autoplay_WhenBothAreHome_FollowsTheDefaultPerson()
    {
        SetState(SpotifyChoice.AutoplayHelper(SpotifyRoom.Keuken, "vincent"), "off");
        SetState(SpotifyChoice.AutoplayHelper(SpotifyRoom.Keuken, "carleen"), "on");

        SetState(SpotifyChoice.DefaultPersonHelper, "Vincent");
        SpotifyChoice.Autoplay(_ha, SpotifyRoom.Keuken, true, true).Should().BeFalse();

        SetState(SpotifyChoice.DefaultPersonHelper, "Carleen");
        SpotifyChoice.Autoplay(_ha, SpotifyRoom.Keuken, true, true).Should().BeTrue();
    }
}
