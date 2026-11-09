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

    public SpotifyChoiceTests() => _ha.GetAllEntities().Returns(_ => _entities.ToList());

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

    [Fact]
    public void TheChosenAccount_BecomesTheSpotcastEntryOfThatAccount()
    {
        AddAccount("111", "01ENTRYVINCENT", "Vincent M");
        AddAccount("222", "01ENTRYCARLEEN", "Carleen M");
        SetState(SpotifyChoice.AccountHelper("carleen"), "carleen m");

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
}
