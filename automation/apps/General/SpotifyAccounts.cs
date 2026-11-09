using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Helpers;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

/// <summary>
/// Keeps the choice of Spotify account on the pages of Vincent and Carleen in line with the accounts that Spotcast has
/// (<c>input_select.spotify_account_{person}</c>): a new account shows up as a choice by itself, a removed one is dropped from it.
/// <para>
/// Linking an account that does not exist yet: the button "Account koppelen" (<c>input_button.spotify_koppelen_{person}</c>) is pressed
/// from the page, which also opens the Spotcast flow in Home Assistant. From that press NetDaemon watches for an account that was not
/// there before (or an account that was linked in one more integration); when it appears it is chosen for that person right away.
/// </para>
/// </summary>
[NetDaemonApp(Id = nameof(SpotifyAccounts))]
public class SpotifyAccounts : BaseApp
{
    public const string LinkButtonPrefix = "input_button.spotify_koppelen_";

    /// <summary>How often a new account is looked for after the button was pressed, and for how long.</summary>
    public static readonly TimeSpan LinkPollInterval = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan LinkGiveUpAfter = TimeSpan.FromMinutes(10);

    private readonly Dictionary<string, IDisposable> _linking = [];
    private readonly AppConfig _config;

    public SpotifyAccounts(
        IHaContext ha,
        ILogger<SpotifyAccounts> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _config = config.Value;
        SyncAll();
        // The helpers can be created a moment after startup, and accounts come and go
        Scheduler.Schedule(TimeSpan.FromMinutes(2), SyncAll);
        Scheduler.SchedulePeriodic(TimeSpan.FromMinutes(15), SyncAll);

        foreach (var person in SpotifyChoice.People)
        {
            var who = person;
            // An input_button's state is the time of the last press; creating it at startup gives "unknown", which is not a press
            new Entity(HaContext, LinkButtonPrefix + who).StateChanges()
                .Where(x => DateTimeOffset.TryParse(x.New?.State, out _))
                .Subscribe(_ => StartLinking(who));
        }
    }

    private static string[] Options(IEnumerable<SpotifyChoice.Account> accounts) =>
        [SpotifyChoice.NoAccount, .. accounts.Select(a => a.Label)];

    private void SyncAll()
    {
        SyncChoices();
        FillPlaylists();
    }

    /// <summary>
    /// A playlist that was never filled in shows what config.json has for that room, so the field is not empty and the person sees (and can
    /// change) what plays now.
    /// </summary>
    private void FillPlaylists()
    {
        try
        {
            foreach (var person in SpotifyChoice.People)
                foreach (var room in System.Enum.GetValues<SpotifyRoom>())
                {
                    var helper = SpotifyChoice.PlaylistHelper(room, person);
                    var state = HaContext.GetState(helper)?.State;
                    // No state at all: the helper is not created yet, the next round catches it
                    if (state is null || !(string.IsNullOrWhiteSpace(state) || state is "unknown" or "unavailable")) continue;

                    var fallback = DefaultFor(room);
                    if (string.IsNullOrEmpty(fallback)) continue;
                    HaContext.CallService("input_text", "set_value", ServiceTarget.FromEntity(helper), new { value = fallback });
                    Logger.LogInformation("Playlist {Helper} set to the one in config.json", helper);
                }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not fill in the playlists");
        }
    }

    private string? DefaultFor(SpotifyRoom room) => room switch
    {
        SpotifyRoom.Douche => _config.SpotifyDoucheUrl,
        SpotifyRoom.Keuken => _config.SpotifyKeukenUrl,
        SpotifyRoom.Radio => _config.SpotifyRadioNlUrl,
        SpotifyRoom.Kerst => _config.SpotifyKerstUrl,
        _ => null
    };

    /// <summary>Sets the choices of both persons to the accounts that exist, and drops a choice that no longer exists.</summary>
    private void SyncChoices()
    {
        try
        {
            var options = Options(SpotifyChoice.Accounts(HaContext));
            foreach (var person in SpotifyChoice.People)
            {
                var helper = SpotifyChoice.AccountHelper(person);
                var state = HaContext.GetState(helper);
                var current = new List<string>();
                if (state?.AttributesJson is { ValueKind: JsonValueKind.Object } a &&
                    a.TryGetProperty("options", out var existing) && existing.ValueKind == JsonValueKind.Array)
                    current = existing.EnumerateArray().Select(x => x.GetString() ?? "").ToList();

                if (!current.SequenceEqual(options))
                {
                    HaContext.CallService("input_select", "set_options", ServiceTarget.FromEntity(helper), new { options });
                    Logger.LogInformation("Spotify accounts for {Person}: {Accounts}", person, string.Join(", ", options));
                }

                // The chosen account is no longer a choice: the same account with other integrations behind it (it got linked in one more
                // place, or lost one) stays chosen under its new name; an account that is gone goes back to the default account
                if (state?.State is { Length: > 0 } chosen && chosen is not ("unknown" or "unavailable") && !options.Contains(chosen))
                {
                    var name = SpotifyChoice.NameOf(chosen);
                    var renamed = options.FirstOrDefault(o => o != SpotifyChoice.NoAccount && string.Equals(SpotifyChoice.NameOf(o), name, StringComparison.OrdinalIgnoreCase));
                    HaContext.CallService("input_select", "select_option", ServiceTarget.FromEntity(helper), new { option = renamed ?? SpotifyChoice.NoAccount });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not sync the Spotify accounts");
        }
    }

    /// <summary>From now on an account that was not there before is the one this person linked.</summary>
    private void StartLinking(string person)
    {
        if (_linking.Remove(person, out var previous)) previous.Dispose();

        var known = SpotifyChoice.Accounts(HaContext).Select(a => a.Label).ToHashSet();
        var started = Scheduler.Now;
        Logger.LogInformation("{Person} is linking a Spotify account, {Known} account(s) known", person, known.Count);

        var poll = Scheduler.SchedulePeriodic(LinkPollInterval, () =>
        {
            try
            {
                // A new account, or an account that got linked in one more integration
                var added = SpotifyChoice.Accounts(HaContext).FirstOrDefault(a => !known.Contains(a.Label));
                if (added != null)
                {
                    SyncChoices();
                    HaContext.CallService("input_select", "select_option", ServiceTarget.FromEntity(SpotifyChoice.AccountHelper(person)), new { option = added.Label });
                    Logger.LogInformation("Linked Spotify account {Name} for {Person}", added.Label, person);
                    Stop(person);
                }
                else if (Scheduler.Now - started >= LinkGiveUpAfter)
                {
                    Logger.LogInformation("No new Spotify account within {Minutes} minutes for {Person}", LinkGiveUpAfter.TotalMinutes, person);
                    Stop(person);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not check for a new Spotify account");
            }
        });
        _linking[person] = poll;
    }

    private void Stop(string person)
    {
        if (_linking.Remove(person, out var poll)) poll.Dispose();
    }
}
