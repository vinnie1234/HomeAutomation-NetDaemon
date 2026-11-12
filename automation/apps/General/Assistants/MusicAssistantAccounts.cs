using System.Reactive.Concurrency;
using System.Text.Json;
using System.Text.RegularExpressions;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Models.Integrations;
using NetDaemon.Client;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Assistants;

/// <summary>
/// Which Music Assistant provider belongs to whom. Music Assistant names every linked account as a provider instance (for example
/// <c>spotify--bc5CaVqE</c>), and playlists are played from the instance of the person. This app looks which instances there are:
/// the first Spotify account is Vincent's, a second Spotify account is Carleen's ("Spotify Carleen"), YouTube Music is Vincent's only.
/// What was filled in by hand (the fields on the Muziek pages) stays; only empty fields are filled.
/// </summary>
[NetDaemonApp(Id = nameof(MusicAssistantAccounts))]
public class MusicAssistantAccounts : BaseApp
{
    public const string SpotifyVincent = "input_text.ma_spotify_vincent";
    public const string SpotifyCarleen = "input_text.ma_spotify_carleen";
    public const string YouTubeVincent = "input_text.ma_ytmusic_vincent";

    private static readonly Regex Instance = new(@"^(?<domain>[a-z_]+)--(?<id>[A-Za-z0-9]+)://", RegexOptions.Compiled);

    private readonly ILogger<MusicAssistantAccounts> _logger;
    private readonly IHomeAssistantRunner _runner;

    public MusicAssistantAccounts(
        IHaContext ha,
        ILogger<MusicAssistantAccounts> logger,
        INotify notify,
        IScheduler scheduler,
        IHomeAssistantRunner runner,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _logger = logger;
        _runner = runner;
        // The helpers can be created a moment after startup, and a second account can be linked at any time
        Scheduler.Schedule(TimeSpan.FromMinutes(2), () => _ = Sync());
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(15), () => _ = Sync());
    }

    private string? Value(string helper)
    {
        var state = HaContext.GetState(helper)?.State;
        return string.IsNullOrWhiteSpace(state) || state is "unknown" or "unavailable" ? null : state;
    }

    /// <summary>The provider instances Music Assistant answers to a search: <c>spotify--abc</c> and so on, per domain.</summary>
    public static Dictionary<string, List<string>> InstancesIn(IEnumerable<string> uris)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var uri in uris)
        {
            var m = Instance.Match(uri);
            if (!m.Success) continue;
            var domain = m.Groups["domain"].Value;
            var instance = $"{domain}--{m.Groups["id"].Value}";
            if (!result.TryGetValue(domain, out var list)) result[domain] = list = [];
            if (!list.Contains(instance)) list.Add(instance);
        }
        return result;
    }

    /// <summary>Fills the empty fields from the instances found; a taken instance is never given to someone else.</summary>
    public static Dictionary<string, string> Assign(Dictionary<string, List<string>> found, string? spotifyVincent, string? spotifyCarleen, string? youTubeVincent)
    {
        var fill = new Dictionary<string, string>();
        var spotify = found.GetValueOrDefault("spotify") ?? [];
        var taken = new[] { spotifyVincent, spotifyCarleen }.Where(x => x != null).ToHashSet();
        var free = spotify.Where(x => !taken.Contains(x)).ToList();

        if (spotifyVincent == null && free.Count > 0) { fill[SpotifyVincent] = free[0]; free.RemoveAt(0); }
        if (spotifyCarleen == null && free.Count > 0) fill[SpotifyCarleen] = free[0];
        if (youTubeVincent == null && found.GetValueOrDefault("ytmusic") is { Count: > 0 } yt) fill[YouTubeVincent] = yt[0];
        return fill;
    }

    private async Task Sync()
    {
        try
        {
            if (HaContext.GetState(SpotifyVincent) == null) return; // the helpers are not there yet

            var configEntry = await MusicAssistantEntry();
            if (configEntry == null) return;

            var uris = new List<string>();
            foreach (var term in new[] { "a", "e" })
            {
                var response = await HaContext.CallServiceWithResponseAsync("music_assistant", "search", data: new Dictionary<string, object>
                {
                    ["config_entry_id"] = configEntry, ["name"] = term, ["media_type"] = new[] { "playlist" }, ["limit"] = 25
                });
                if (response?.TryGetProperty("playlists", out var list) == true && list.ValueKind == JsonValueKind.Array)
                    uris.AddRange(list.EnumerateArray().Select(x => x.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : ""));
            }

            var fill = Assign(InstancesIn(uris), Value(SpotifyVincent), Value(SpotifyCarleen), Value(YouTubeVincent));
            foreach (var (helper, instance) in fill)
            {
                new InputTextEntity(HaContext, helper).SetValue(instance);
                _logger.LogInformation("Music Assistant: {Helper} is {Instance}", helper, instance);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not look up the Music Assistant accounts");
        }
    }

    /// <summary>The config entry id of the Music Assistant integration.</summary>
    private async Task<string?> MusicAssistantEntry()
    {
        var connection = _runner.CurrentConnection;
        if (connection == null) return null;
        var entries = await connection.SendCommandAndReturnResponseAsync<GetConfigEntriesCommand, IReadOnlyList<ConfigEntry>>(
            new GetConfigEntriesCommand(), CancellationToken.None) ?? [];
        return entries.FirstOrDefault(e => e.Domain == "music_assistant" && e.DisabledBy is null)?.EntryId;
    }
}
