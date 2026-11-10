using System.Text.Json;
using System.Text.RegularExpressions;
using Automation.Configuration;

namespace Automation.Helpers;

/// <summary>The places where music can be started, each with its own playlist.</summary>
public enum SpotifyRoom
{
    /// <summary>The bathroom while somebody takes a shower.</summary>
    Douche,

    /// <summary>The kitchen speaker when somebody walks into the living room.</summary>
    Keuken,

    /// <summary>The bathroom when the person who is home does not have a shower playlist: the radio.</summary>
    Radio,

    /// <summary>The Christmas playlist.</summary>
    Kerst
}

/// <summary>
/// Which playlist and which Spotify account (a Spotcast account) a room plays. The account is chosen from the accounts of all three
/// Spotify integrations; it plays when it is linked in Spotcast, otherwise Spotcast takes its default account. Vincent and Carleen each set their own on their own page
/// (<c>input_text.spotify_{room}_{person}</c> and <c>input_select.spotify_account_{person}</c>); whose settings count depends on who is home,
/// and when both are home on <see cref="DefaultPersonHelper"/>. What nobody has set falls back to config.json.
/// </summary>
public static class SpotifyChoice
{
    /// <summary>The choice for an account that was not linked: Spotcast then takes its default account.</summary>
    public const string NoAccount = "Standaard account";

    /// <summary>Whose settings count while both are home (Vincent or Carleen).</summary>
    public const string DefaultPersonHelper = "input_select.spotify_standaard_persoon";

    public static readonly string[] People = ["vincent", "carleen"];

    private static readonly Regex ProfileSensor = new(@"^sensor\.spotcast_[^_]+_spotify_profile$", RegexOptions.Compiled);
    private static readonly Regex Brackets = new(@"\s*\([^()]*\)\s*$", RegexOptions.Compiled);

    public static string PlaylistHelper(SpotifyRoom room, string person) => $"input_text.spotify_{room.ToString().ToLowerInvariant()}_{person}";

    /// <summary>The switch of a person for a room: whether the automations (shower, kitchen) may start music with that person's settings.</summary>
    public static string AutoplayHelper(SpotifyRoom room, string person) => $"input_boolean.spotify_auto_{room.ToString().ToLowerInvariant()}_{person}";

    /// <summary>
    /// Whether an automation may start the music of a room: the switch of the person whose settings count (see <see cref="PersonFor"/>) for that
    /// room is not off. A switch that does not exist yet counts as on.
    /// </summary>
    public static bool Autoplay(IHaContext ha, SpotifyRoom room, bool vincentHome, bool carleenHome) =>
        Value(ha, AutoplayHelper(room, PersonFor(ha, vincentHome, carleenHome))) != "off";

    public static string AccountHelper(string person) => $"input_select.spotify_account_{person}";

    /// <summary>
    /// A Spotify account and where it is linked: Spotcast (the config entry it lives in, which is what <c>spotcast.play_media</c> calls the
    /// account; null when not linked there), the Spotify integration and SpotifyPlus. Music is always played through Spotcast.
    /// </summary>
    public sealed record Account(string Name, string? SpotcastEntryId, bool Spotify, bool SpotifyPlus)
    {
        /// <summary>The account is linked in Spotcast, so it can play.</summary>
        public bool InSpotcast => SpotcastEntryId != null;

        /// <summary>What the choice shows: the name and the integrations it is linked in.</summary>
        public string Label => $"{Name} ({string.Join(", ", new[] { InSpotcast ? "Spotcast" : null, Spotify ? "Spotify" : null, SpotifyPlus ? "SpotifyPlus" : null }.OfType<string>())})";
    }

    /// <summary>The name of an account in a choice (without the integrations behind it).</summary>
    public static string NameOf(string label) => Brackets.Replace(label.Trim(), "");

    /// <summary>
    /// The Spotify accounts that exist now, merged by name from the three integrations: the profile sensor of each Spotcast account and the
    /// media player of each account in the Spotify and SpotifyPlus integrations.
    /// </summary>
    public static List<Account> Accounts(IHaContext ha)
    {
        var spotcast = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var spotify = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spotifyPlus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in ha.GetAllEntities() ?? [])
        {
            if (entity.EntityState?.AttributesJson is not { ValueKind: JsonValueKind.Object } a) continue;
            if (ProfileSensor.IsMatch(entity.EntityId))
            {
                var entryId = Text(a, "entry_id");
                var name = Text(a, "display_name");
                if (!string.IsNullOrEmpty(entryId) && !string.IsNullOrEmpty(name)) spotcast.TryAdd(name, entryId);
            }
            else if (entity.EntityId.StartsWith("media_player.spotifyplus_", StringComparison.Ordinal))
                AddName(spotifyPlus, Text(a, "friendly_name"), "SpotifyPlus ");
            else if (entity.EntityId.StartsWith("media_player.spotify_", StringComparison.Ordinal))
                AddName(spotify, Text(a, "friendly_name"), "Spotify ");
        }

        return spotcast.Keys.Concat(spotify).Concat(spotifyPlus).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new Account(n, spotcast.GetValueOrDefault(n), spotify.Contains(n), spotifyPlus.Contains(n)))
            .ToList();
    }

    private static void AddName(HashSet<string> names, string? friendlyName, string prefix)
    {
        if (friendlyName != null && friendlyName.StartsWith(prefix, StringComparison.Ordinal) && friendlyName.Length > prefix.Length)
            names.Add(friendlyName[prefix.Length..].Trim());
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>
    /// The playlist and the account to play in a room. The account is null when the person did not choose one (or the chosen one is gone):
    /// Spotcast then takes its default account. The playlist is null when neither the person nor config.json has one.
    /// </summary>
    public static (string? Url, string? Account) Resolve(IHaContext ha, AppConfig config, SpotifyRoom room, bool vincentHome, bool carleenHome)
    {
        var person = PersonFor(ha, vincentHome, carleenHome);
        var url = Value(ha, PlaylistHelper(room, person)) ?? Fallback(config, room, carleenHome);
        var chosen = Value(ha, AccountHelper(person));
        var account = chosen is null || chosen == NoAccount
            ? null
            : Accounts(ha).FirstOrDefault(x => string.Equals(x.Name, NameOf(chosen), StringComparison.OrdinalIgnoreCase))?.SpotcastEntryId;
        return (url, account);
    }

    /// <summary>Whose settings count: the one who is home alone, otherwise the default person.</summary>
    public static string PersonFor(IHaContext ha, bool vincentHome, bool carleenHome)
    {
        if (carleenHome && !vincentHome) return "carleen";
        if (vincentHome && !carleenHome) return "vincent";
        return string.Equals(Value(ha, DefaultPersonHelper), "Carleen", StringComparison.OrdinalIgnoreCase) ? "carleen" : "vincent";
    }

    /// <summary>What config.json has for a room: the same as before the settings existed.</summary>
    private static string? Fallback(AppConfig config, SpotifyRoom room, bool carleenHome) => room switch
    {
        SpotifyRoom.Douche => carleenHome ? config.SpotifyDoucheUrl : config.SpotifyRadioNlUrl,
        SpotifyRoom.Keuken => config.SpotifyKeukenUrl,
        SpotifyRoom.Radio => config.SpotifyRadioNlUrl,
        SpotifyRoom.Kerst => config.SpotifyKerstUrl,
        _ => null
    };

    private static string? Value(IHaContext ha, string entityId)
    {
        var state = ha.GetState(entityId)?.State?.Trim();
        return string.IsNullOrEmpty(state) || state is "unknown" or "unavailable" ? null : state;
    }
}
