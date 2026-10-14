using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.DiscordNotificationModels;
using Automation.Models.Integrations;
using Microsoft.Extensions.Options;
using NetDaemon.Client;

namespace Automation.apps.General;

/// <summary>
/// Watches the Home Assistant integrations every quarter of an hour. An integration has a problem when it failed
/// to load, or when it is loaded but did not load some of its entities. Such an integration is reloaded up to
/// <see cref="MaxReloadAttempts"/> times, with <see cref="RetryDelay"/> in between. When that does not help, the
/// Discord log channel is told which integration has problems and what they are. The attempts are stored, so a
/// restart of NetDaemon doesn't reset them; they are removed once the integration works again.
/// </summary>
[NetDaemonApp(Id = nameof(IntegrationMonitor))]
public class IntegrationMonitor : BaseApp
{
    /// <summary>The storage key of the reload history.</summary>
    public const string StorageKey = "IntegrationMonitor";

    /// <summary>How often an integration with problems is reloaded before Discord is notified.</summary>
    public const int MaxReloadAttempts = 2;

    /// <summary>With more integrations with problems, something else is wrong (e.g. the network), so nothing is reloaded.</summary>
    public const int MaxFailingIntegrations = 5;

    /// <summary>How long to wait after a reload before checking whether it helped.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    /// <summary>The maximum number of entities listed in the Discord notification.</summary>
    private const int MaxListedEntities = 10;

    private readonly IHomeAssistantRunner _runner;
    private readonly IDataRepository _storage;
    private readonly AppConfig _config;

    /// <summary>Guards <see cref="_inProgress"/> and the stored reload history.</summary>
    private readonly object _lock = new();

    /// <summary>The integrations that are being repaired right now, so the next check doesn't repair them twice.</summary>
    private readonly HashSet<string> _inProgress = [];

    /// <summary>
    /// An integration with problems.
    /// </summary>
    /// <param name="Entry">The integration.</param>
    /// <param name="NotLoadedEntities">The enabled entities of the integration that it did not load.</param>
    private sealed record IntegrationProblem(ConfigEntry Entry, IReadOnlyList<string> NotLoadedEntities)
    {
        /// <summary>Gets a short description of the problem, for the log and Discord.</summary>
        public string Description => Entry.HasFailed
            ? $"{Entry.State}: {(string.IsNullOrWhiteSpace(Entry.Reason) ? "geen foutmelding van Home Assistant" : Entry.Reason)}"
            : $"geladen, maar {NotLoadedEntities.Count} entiteit(en) niet geladen";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationMonitor"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for the checks and the retry delay.</param>
    /// <param name="storage">The data repository holding the reload history.</param>
    /// <param name="runner">The Home Assistant runner, whose websocket connection is used to read the integrations and the entity registry.</param>
    /// <param name="config">The app configuration with the Discord channels.</param>
    public IntegrationMonitor(
        IHaContext ha,
        ILogger<IntegrationMonitor> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage,
        IHomeAssistantRunner runner,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler)
    {
        _storage = storage;
        _runner = runner;
        _config = config.Value;

        // At :00, :15, :30 and :45
        scheduler.ScheduleCronNl("*/15 * * * *", () => _ = CheckIntegrationsAsync());
    }

    /// <summary>
    /// Finds the integrations with problems and starts repairing the ones that aren't being repaired yet.
    /// </summary>
    private async Task CheckIntegrationsAsync()
    {
        try
        {
            var problems = await GetProblemsAsync();

            ForgetRecoveredIntegrations(problems);

            if (problems.Count > MaxFailingIntegrations)
            {
                Logger.LogWarning("{Count} integrations have problems, more than {Max}: not reloading them, something else is wrong ({Integrations})",
                    problems.Count, MaxFailingIntegrations, string.Join(", ", problems.Select(p => p.Entry.Title)));
                return;
            }

            foreach (var problem in problems)
                StartRepair(problem);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while checking the integrations");
        }
    }

    /// <summary>
    /// Starts repairing an integration with problems, unless it is being repaired already.
    /// </summary>
    /// <param name="problem">The integration with problems.</param>
    private void StartRepair(IntegrationProblem problem)
    {
        lock (_lock)
        {
            if (!_inProgress.Add(problem.Entry.EntryId)) return;
        }

        ReloadOrNotify(problem);
    }

    /// <summary>
    /// Reloads an integration with problems and checks after <see cref="RetryDelay"/> whether that helped.
    /// When the reload attempts are used up, Discord is notified once and the repair ends.
    /// </summary>
    /// <param name="problem">The integration with problems.</param>
    private void ReloadOrNotify(IntegrationProblem problem)
    {
        var entry = problem.Entry;
        try
        {
            var record = GetRecord(entry.EntryId);
            if (record?.ReloadAttempts >= MaxReloadAttempts)
            {
                if (!record.Notified) NotifyFailing(problem, record);
                EndRepair(entry.EntryId);
                return;
            }

            var attempt = (record?.ReloadAttempts ?? 0) + 1;
            Logger.LogWarning("Integration {Title} ({Domain}) has problems ({Problem}): reloading it (attempt {Attempt} of {Max})",
                entry.Title, entry.Domain, problem.Description, attempt, MaxReloadAttempts);

            SaveRecord(new IntegrationRepairRecord
            {
                EntryId = entry.EntryId,
                Domain = entry.Domain,
                Title = entry.Title,
                ReloadAttempts = attempt,
                LastReloadAttempt = Scheduler.Now
            });
            HaContext.CallService("homeassistant", "reload_config_entry", data: new { entry_id = entry.EntryId });

            Scheduler.Schedule(RetryDelay, () => _ = CheckRepairAsync(entry, attempt));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while repairing integration {Title} ({Domain})", entry.Title, entry.Domain);
            EndRepair(entry.EntryId);
        }
    }

    /// <summary>
    /// Checks whether a reload fixed an integration; reloads it again (or notifies Discord) when it did not.
    /// </summary>
    /// <param name="entry">The reloaded integration.</param>
    /// <param name="attempt">The reload attempt that is checked.</param>
    private async Task CheckRepairAsync(ConfigEntry entry, int attempt)
    {
        try
        {
            var current = (await GetProblemsAsync()).FirstOrDefault(p => p.Entry.EntryId == entry.EntryId);
            if (current is not null)
            {
                ReloadOrNotify(current);
                return;
            }

            Logger.LogInformation("Integration {Title} ({Domain}) works again after reload attempt {Attempt}", entry.Title, entry.Domain, attempt);
            RemoveRecord(entry.EntryId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while checking the reload of integration {Title} ({Domain})", entry.Title, entry.Domain);
        }

        EndRepair(entry.EntryId);
    }

    private void EndRepair(string entryId)
    {
        lock (_lock)
        {
            _inProgress.Remove(entryId);
        }
    }

    /// <summary>
    /// Tells the Discord log channel that reloading did not fix an integration, and remembers it was told.
    /// </summary>
    /// <param name="problem">The integration that still has problems.</param>
    /// <param name="record">The reload history of the integration.</param>
    private void NotifyFailing(IntegrationProblem problem, IntegrationRepairRecord record)
    {
        var entry = problem.Entry;
        var fields = new List<Field>
        {
            new() { Name = "Integratie", Value = $"{entry.Title} ({entry.Domain})" },
            new() { Name = "Probleem", Value = problem.Description },
            new() { Name = "Herlaadpogingen", Value = record.ReloadAttempts.ToString() }
        };

        if (problem.NotLoadedEntities.Count > 0)
        {
            var listed = string.Join("\n", problem.NotLoadedEntities.Take(MaxListedEntities));
            if (problem.NotLoadedEntities.Count > MaxListedEntities) listed += $"\n... en nog {problem.NotLoadedEntities.Count - MaxListedEntities}";
            fields.Add(new Field { Name = $"Niet geladen entiteiten ({problem.NotLoadedEntities.Count})", Value = listed });
        }

        Logger.LogError("Integration {Title} ({Domain}) still has problems after {Attempts} reloads: {Problem}",
            entry.Title, entry.Domain, record.ReloadAttempts, problem.Description);

        Notify.NotifyDiscord("", [_config.Discord.Logs], new DiscordNotificationModel
        {
            Embed = new Embed
            {
                Title = $"Integratie {entry.Title} heeft problemen",
                Description = $"Na {record.ReloadAttempts} keer herladen werkt de integratie nog steeds niet goed.",
                Url = $"{_config.BaseUrlHomeAssistant}/config/integrations/integration/{entry.Domain}",
                Fields = fields.ToArray()
            }
        });

        SaveRecord(record with { Notified = true });
    }

    /// <summary>
    /// Gets the enabled integrations that failed to load, or that are loaded but did not load all their entities.
    /// </summary>
    private async Task<IReadOnlyList<IntegrationProblem>> GetProblemsAsync()
    {
        var entries = await Connection.SendCommandAndReturnResponseAsync<GetConfigEntriesCommand, IReadOnlyList<ConfigEntry>>(
            new GetConfigEntriesCommand(), CancellationToken.None) ?? [];
        var notLoaded = (await GetEntityRegistryAsync())
            .Where(e => e.ConfigEntryId is not null && e.DisabledBy is null && IsNotLoaded(e.EntityId))
            .ToLookup(e => e.ConfigEntryId!, e => e.EntityId);

        return entries
            .Where(e => e.DisabledBy is null)
            .Select(e => new IntegrationProblem(e, notLoaded[e.EntryId].Order().ToList()))
            .Where(p => p.Entry.HasFailed || (p.Entry.State == "loaded" && p.NotLoadedEntities.Count > 0))
            .ToList();
    }

    /// <summary>
    /// Gets the entity registry; empty when it could not be read, so failed integrations are still found.
    /// </summary>
    private async Task<IReadOnlyList<EntityRegistryEntry>> GetEntityRegistryAsync()
    {
        try
        {
            return await Connection.SendCommandAndReturnResponseAsync<GetEntityRegistryCommand, IReadOnlyList<EntityRegistryEntry>>(
                new GetEntityRegistryCommand(), CancellationToken.None) ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not read the entity registry");
            return [];
        }
    }

    /// <summary>
    /// Gets whether the integration did not load an entity. Home Assistant then shows the entity from the registry
    /// as unavailable with the <c>restored</c> attribute. An entity that is merely unavailable (e.g. a device that
    /// is offline) was loaded, and reloading the integration won't help it.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    private bool IsNotLoaded(string entityId) =>
        HaContext.GetState(entityId) is { State: "unavailable", AttributesJson: { ValueKind: JsonValueKind.Object } attributes } &&
        attributes.TryGetProperty("restored", out var restored) &&
        restored.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Removes the reload history of integrations that work again, so they get all attempts when they fail later.
    /// </summary>
    /// <param name="problems">The integrations that have problems right now.</param>
    private void ForgetRecoveredIntegrations(IReadOnlyCollection<IntegrationProblem> problems)
    {
        lock (_lock)
        {
            var records = GetRecords();
            var recovered = records.Keys
                .Where(id => !_inProgress.Contains(id) && problems.All(p => p.Entry.EntryId != id))
                .ToList();
            if (recovered.Count == 0) return;

            foreach (var id in recovered)
            {
                Logger.LogInformation("Integration {Title} works again: forgetting its reload attempts", records[id].Title);
                records.Remove(id);
            }

            _storage.Save(StorageKey, records);
        }
    }

    /// <summary>
    /// Gets the current websocket connection to Home Assistant.
    /// </summary>
    private IHomeAssistantConnection Connection =>
        _runner.CurrentConnection ?? throw new InvalidOperationException("Not connected to Home Assistant");

    private Dictionary<string, IntegrationRepairRecord> GetRecords() =>
        _storage.Get<Dictionary<string, IntegrationRepairRecord>>(StorageKey) ?? [];

    private IntegrationRepairRecord? GetRecord(string entryId)
    {
        lock (_lock)
        {
            return GetRecords().GetValueOrDefault(entryId);
        }
    }

    private void SaveRecord(IntegrationRepairRecord record)
    {
        lock (_lock)
        {
            var records = GetRecords();
            records[record.EntryId] = record;
            _storage.Save(StorageKey, records);
        }
    }

    private void RemoveRecord(string entryId)
    {
        lock (_lock)
        {
            var records = GetRecords();
            if (records.Remove(entryId)) _storage.Save(StorageKey, records);
        }
    }
}
