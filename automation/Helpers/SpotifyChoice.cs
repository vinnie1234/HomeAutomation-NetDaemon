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
/// Which playlist and which Spotify account (a Spotcast account) a room plays. Vincent and Carleen each set their own on their own page
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

    public static string PlaylistHelper(SpotifyRoom room, string person) => $"input_text.spotify_{room.ToString().ToLowerInvariant()}_{person}";

    public static string AccountHelper(string person) => $"input_select.spotify_account_{person}";

    /// <summary>A Spotcast account: the config entry it lives in (what <c>spotcast.play_media</c> calls the account) and the Spotify name.</summary>
    public sealed record Account(string EntryId, string Name);

    /// <summary>The Spotcast accounts that exist now, from the profile sensor each of them has.</summary>
    public static List<Account> Accounts(IHaContext ha)
    {
        var accounts = new List<Account>();
        foreach (var entity in ha.GetAllEntities() ?? [])
        {
            if (!ProfileSensor.IsMatch(entity.EntityId)) continue;
            if (entity.EntityState?.AttributesJson is not { ValueKind: JsonValueKind.Object } a) continue;
            var entryId = a.TryGetProperty("entry_id", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            var name = a.TryGetProperty("display_name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (!string.IsNullOrEmpty(entryId) && !string.IsNullOrEmpty(name) && accounts.All(x => x.EntryId != entryId))
                accounts.Add(new Account(entryId, name));
        }
        return accounts.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

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
            : Accounts(ha).FirstOrDefault(x => string.Equals(x.Name, chosen, StringComparison.OrdinalIgnoreCase))?.EntryId;
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
