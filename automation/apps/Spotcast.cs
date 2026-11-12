namespace Automation.apps;

/// <summary>
/// Starts a playlist on a speaker. With the switch "Gebruik Music Assistant" on it goes through Music Assistant (the speaker is swapped for its
/// Music Assistant player, the Spotify account is the one linked in Music Assistant); with it off, or for a speaker Music Assistant does not know,
/// through Spotcast like before.
/// </summary>
public class Spotcast(IHaContext rawHa) : ISpotcast
{
    /// <summary>The switch in Beheer: use Music Assistant instead of Spotcast.</summary>
    public const string UseMusicAssistant = "input_boolean.gebruik_music_assistant";

    /// <summary>The speakers (Cast, Google Home) and the Music Assistant player of the same speaker.</summary>
    public static readonly IReadOnlyDictionary<string, string> MusicAssistantPlayers = new Dictionary<string, string>
    {
        ["media_player.nestmini9818"] = "media_player.keuken_keuken",
        ["media_player.woonkamer"] = "media_player.woonkamer_woonkamer",
        ["media_player.googlehome0351"] = "media_player.badkamer_badkamer_speaker",
        ["media_player.friends_speakers"] = "media_player.gang_friends_speakers",
        ["media_player.hele_huis"] = "media_player.hele_huis_2",
        ["media_player.hub_vincent"] = "media_player.slaapkamer_hub_vincent",
    };

    // What this plays is written to the activity log as the app Spotcast
    private readonly IHaContext _ha = new Core.ActivityHaContext(rawHa, nameof(Spotcast));
    private readonly Entities _entities = new(rawHa);

    /// <summary>Whether Music Assistant is switched on in Beheer.</summary>
    public static bool IsMusicAssistantOn(IHaContext ha) => ha.GetState(UseMusicAssistant)?.State == "on";

    /// <summary>A Spotify link or URI as Music Assistant writes it: <c>spotify://playlist/id</c>. Anything else (a Music Assistant URI, another link) stays as it is.</summary>
    public static string ToMusicAssistantUri(string url, string? instance = null)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"(?:open\.spotify\.com/(?:intl-[a-z]+/)?|spotify:)(?<type>playlist|album|track|artist)[/:](?<id>[A-Za-z0-9]+)");
        return match.Success ? $"{(string.IsNullOrWhiteSpace(instance) ? "spotify" : instance)}://{match.Groups["type"].Value}/{match.Groups["id"].Value}" : url;
    }

    /// <summary>The Spotify account in Music Assistant of whoever's settings count now (see <see cref="Helpers.SpotifyChoice.PersonFor"/>).</summary>
    private string? SpotifyInstance()
    {
        var person = Helpers.SpotifyChoice.PersonFor(rawHa, _entities.InputBoolean.Awayvincent.IsOff(), _entities.InputBoolean.Awaycarleen.IsOff());
        var state = rawHa.GetState($"input_text.ma_spotify_{person}")?.State;
        return string.IsNullOrWhiteSpace(state) || state is "unknown" or "unavailable" ? null : state;
    }

    public void PlaySpotify(MediaPlayerEntity mediaPlayer, string spotifyUrl, string? account = null)
    {
        // Don't play music while Vincent and Carleen are both away (e.g. only a house sitter is home)
        if (_entities.InputBoolean.Away.IsOn()) return;

        if (IsMusicAssistantOn(rawHa) && MusicAssistantPlayers.TryGetValue(mediaPlayer.EntityId, out var player))
        {
            PlayOnMusicAssistant(player, spotifyUrl);
            return;
        }

        var deviceId = mediaPlayer.Registration?.Device?.Id;

        if (deviceId != null)
        {
            var serviceData = new Dictionary<string, object>
            {
                { "media_player", new Dictionary<string, object> {
                    { "device_id", deviceId }
                }},
                { "spotify_uri", spotifyUrl },
                { "data", new Dictionary<string, object> {
                    { "shuffle", true }
                }}
            };

            if (!string.IsNullOrEmpty(account)) serviceData["account"] = account;

            _ha.CallService("spotcast", "play_media", null, serviceData);
        }
    }

    /// <summary>What a Music Assistant URI is (<c>playlist</c>, <c>album</c>, <c>radio</c>, ...); playlist when it does not say.</summary>
    public static string MediaTypeOf(string uri)
    {
        var m = System.Text.RegularExpressions.Regex.Match(uri, @"^[a-z0-9_\-]+://(?<type>playlist|album|artist|track|radio)/");
        return m.Success ? m.Groups["type"].Value : "playlist";
    }

    private void PlayOnMusicAssistant(string player, string url)
    {
        var target = ServiceTarget.FromEntity(player);
        // The shuffle belongs to the queue of the player and stays when the queue is replaced
        _ha.CallService("media_player", "shuffle_set", target, new { shuffle = true });
        var uri = ToMusicAssistantUri(url, SpotifyInstance());
        _ha.CallService("music_assistant", "play_media", target,
            new { media_id = uri, media_type = MediaTypeOf(uri), enqueue = "replace", radio_mode = false });
    }
}
