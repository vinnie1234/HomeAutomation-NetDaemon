using System.IO;
using System.Text.Json;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.DiscordNotificationModels;
using Automation.Models.Integrations;
using Automation.Repository;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.Client;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class IntegrationMonitorTests : IDisposable
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();
    private readonly IHomeAssistantRunner _runner = Substitute.For<IHomeAssistantRunner>();
    private readonly IHomeAssistantConnection _connection = Substitute.For<IHomeAssistantConnection>();
    private readonly INotify _notify = Substitute.For<INotify>();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"IntegrationMonitorTests_{Guid.NewGuid():N}");
    private readonly IDataRepository _storage;
    private IReadOnlyList<ConfigEntry> _entries = [];
    private IReadOnlyList<EntityRegistryEntry> _registry = [];

    public IntegrationMonitorTests()
    {
        _runner.CurrentConnection.Returns(_connection);
        Directory.CreateDirectory(_storagePath);
        _storage = new DataRepository(_storagePath, Substitute.For<ILogger>());

        _connection.SendCommandAndReturnResponseAsync<GetConfigEntriesCommand, IReadOnlyList<ConfigEntry>>(
                Arg.Any<GetConfigEntriesCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ConfigEntry>?>(_entries));
        _connection.SendCommandAndReturnResponseAsync<GetEntityRegistryCommand, IReadOnlyList<EntityRegistryEntry>>(
                Arg.Any<GetEntityRegistryCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<EntityRegistryEntry>?>(_registry));
    }

    public void Dispose()
    {
        _ctx.Dispose();
        Directory.Delete(_storagePath, recursive: true);
    }

    [Fact]
    public void FailingIntegration_RecoversAfterReload_IsReloadedOnceAndForgotten()
    {
        _entries = [Failing("hue")];
        CreateApp();

        AdvanceToFirstCheck();
        VerifyReloads("hue", 1);
        Records().Should().ContainKey("hue").WhoseValue.ReloadAttempts.Should().Be(1);

        _entries = [Loaded("hue")];
        Advance(IntegrationMonitor.RetryDelay);

        VerifyReloads("hue", 1);
        Records().Should().BeEmpty();
        _notify.DidNotReceiveWithAnyArgs().NotifyDiscord(default!, default!);
    }

    [Fact]
    public void FailingIntegration_RecoversAfterSecondReload_IsForgotten()
    {
        _entries = [Failing("hue")];
        CreateApp();

        AdvanceToFirstCheck();
        Advance(IntegrationMonitor.RetryDelay);
        VerifyReloads("hue", 2);

        _entries = [Loaded("hue")];
        Advance(IntegrationMonitor.RetryDelay);

        Records().Should().BeEmpty();
        _notify.DidNotReceiveWithAnyArgs().NotifyDiscord(default!, default!);
    }

    [Fact]
    public void FailingIntegration_StillFailingAfterTwoReloads_NotifiesLogChannelOnce()
    {
        _entries = [Failing("hue", reason: "Unable to connect to the bridge")];
        _registry = [new EntityRegistryEntry { EntityId = "light.woonkamer", ConfigEntryId = "hue" }];
        SetState("light.woonkamer", "unavailable", restored: true);
        CreateApp();

        AdvanceToFirstCheck();
        VerifyReloads("hue", 1);

        // The second reload waits for the retry delay
        Advance(IntegrationMonitor.RetryDelay - TimeSpan.FromSeconds(1));
        VerifyReloads("hue", 1);
        Advance(TimeSpan.FromSeconds(1));
        VerifyReloads("hue", 2);

        Advance(IntegrationMonitor.RetryDelay);

        _notify.Received(1).NotifyDiscord("", Arg.Is<string[]>(t => t.Single() == "logs_channel"),
            Arg.Is<DiscordNotificationModel>(m =>
                m.Embed!.Title!.Contains("Hue") &&
                m.Embed.Fields!.Any(f => f.Value.Contains("Unable to connect to the bridge")) &&
                m.Embed.Fields!.Any(f => f.Value.Contains("light.woonkamer"))));
        Records()["hue"].Notified.Should().BeTrue();

        // Later checks neither reload nor notify again
        Advance(TimeSpan.FromHours(1));
        VerifyReloads("hue", 2);
        _notify.ReceivedWithAnyArgs(1).NotifyDiscord(default!, default!);
    }

    [Fact]
    public void StoredAttempts_SurviveRestart_OnlyRemainingAttemptIsUsed()
    {
        _storage.Save(IntegrationMonitor.StorageKey, new Dictionary<string, IntegrationRepairRecord>
        {
            ["hue"] = new() { EntryId = "hue", Domain = "hue", Title = "Hue", ReloadAttempts = 1 }
        });
        _entries = [Failing("hue")];
        CreateApp();

        AdvanceToFirstCheck();
        VerifyReloads("hue", 1);

        Advance(IntegrationMonitor.RetryDelay);
        _notify.ReceivedWithAnyArgs(1).NotifyDiscord(default!, default!);
    }

    [Fact]
    public void NotifiedIntegration_LoadsAgain_GetsAllAttemptsWhenItFailsLater()
    {
        _storage.Save(IntegrationMonitor.StorageKey, new Dictionary<string, IntegrationRepairRecord>
        {
            ["hue"] = new() { EntryId = "hue", Domain = "hue", Title = "Hue", ReloadAttempts = 2, Notified = true }
        });
        _entries = [Loaded("hue")];
        CreateApp();

        AdvanceToFirstCheck();
        Records().Should().BeEmpty();

        _entries = [Failing("hue")];
        Advance(TimeSpan.FromMinutes(15));
        VerifyReloads("hue", 1);
    }

    [Fact]
    public void MoreThanFiveFailingIntegrations_ReloadsNothing()
    {
        _entries = Enumerable.Range(1, 6).Select(i => Failing($"entry{i}")).ToList();
        CreateApp();

        AdvanceToFirstCheck();

        _ctx.HaContext.DidNotReceive().CallService("homeassistant", "reload_config_entry", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
        _notify.DidNotReceiveWithAnyArgs().NotifyDiscord(default!, default!);
    }

    [Fact]
    public void FiveFailingIntegrations_AreAllReloaded()
    {
        _entries = Enumerable.Range(1, 5).Select(i => Failing($"entry{i}")).ToList();
        CreateApp();

        AdvanceToFirstCheck();

        _ctx.HaContext.Received(5).CallService("homeassistant", "reload_config_entry", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void DisabledOrLoadedIntegrations_AreIgnored()
    {
        _entries =
        [
            Loaded("hue"),
            Failing("disabled") with { State = "not_loaded", DisabledBy = "user" },
            Failing("starting") with { State = "setup_in_progress" }
        ];
        CreateApp();

        AdvanceToFirstCheck();

        _ctx.HaContext.DidNotReceive().CallService("homeassistant", "reload_config_entry", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void LoadedIntegration_WithEntitiesThatDidNotLoad_IsReloadedUntilTheyLoad()
    {
        _entries = [Loaded("hue")];
        _registry =
        [
            new EntityRegistryEntry { EntityId = "light.woonkamer", ConfigEntryId = "hue" },
            new EntityRegistryEntry { EntityId = "light.keuken", ConfigEntryId = "hue" }
        ];
        SetState("light.woonkamer", "on");
        SetState("light.keuken", "unavailable", restored: true);
        CreateApp();

        AdvanceToFirstCheck();
        VerifyReloads("hue", 1);

        SetState("light.keuken", "off");
        Advance(IntegrationMonitor.RetryDelay);

        VerifyReloads("hue", 1);
        Records().Should().BeEmpty();
    }

    [Fact]
    public void LoadedIntegration_WithEntitiesThatStillDidNotLoad_NotifiesWhichEntities()
    {
        _entries = [Loaded("hue")];
        _registry = [new EntityRegistryEntry { EntityId = "light.keuken", ConfigEntryId = "hue" }];
        SetState("light.keuken", "unavailable", restored: true);
        CreateApp();

        AdvanceToFirstCheck();
        Advance(IntegrationMonitor.RetryDelay * 2);

        VerifyReloads("hue", 2);
        _notify.Received(1).NotifyDiscord("", Arg.Any<string[]>(),
            Arg.Is<DiscordNotificationModel>(m =>
                m.Embed!.Fields!.Any(f => f.Value.Contains("1 entiteit(en) niet geladen")) &&
                m.Embed.Fields!.Any(f => f.Value == "light.keuken")));
    }

    [Fact]
    public void LoadedIntegration_WithOfflineOrDisabledEntities_IsIgnored()
    {
        _entries = [Loaded("zha")];
        _registry =
        [
            // Loaded by the integration, but the device is offline: a reload won't help
            new EntityRegistryEntry { EntityId = "sensor.deur_batterij", ConfigEntryId = "zha" },
            new EntityRegistryEntry { EntityId = "sensor.uitgezet", ConfigEntryId = "zha", DisabledBy = "user" }
        ];
        SetState("sensor.deur_batterij", "unavailable");
        SetState("sensor.uitgezet", "unavailable", restored: true);
        CreateApp();

        AdvanceToFirstCheck();

        _ctx.HaContext.DidNotReceive().CallService("homeassistant", "reload_config_entry", Arg.Any<ServiceTarget?>(), Arg.Any<object?>());
    }

    [Fact]
    public void Checks_RunEveryQuarterOfAnHour()
    {
        CreateApp();

        // The test clock starts at 01:00 Dutch time, so the first check is at 01:15
        Advance(TimeSpan.FromMinutes(14));
        ConfigEntryRequests().Should().Be(0);
        Advance(TimeSpan.FromMinutes(1));
        ConfigEntryRequests().Should().Be(1);
        Advance(TimeSpan.FromMinutes(45));
        ConfigEntryRequests().Should().Be(4);
    }

    private void CreateApp() =>
        _ = new IntegrationMonitor(_ctx.HaContext, Substitute.For<ILogger<IntegrationMonitor>>(), _notify, _ctx.Scheduler,
            _storage, _runner, Options.Create(new AppConfig { Discord = new DiscordConfig { Logs = "logs_channel" } }));

    private void SetState(string entityId, string state, bool restored = false) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState
        {
            EntityId = entityId,
            State = state,
            AttributesJson = JsonSerializer.SerializeToElement(restored ? new { restored = true } : new object())
        });

    private void AdvanceToFirstCheck() => Advance(TimeSpan.FromMinutes(15));

    private void Advance(TimeSpan time) => _ctx.AdvanceTimeBy(time.Ticks);

    private Dictionary<string, IntegrationRepairRecord> Records() =>
        _storage.Get<Dictionary<string, IntegrationRepairRecord>>(IntegrationMonitor.StorageKey) ?? [];

    private int ConfigEntryRequests() =>
        _connection.ReceivedCalls().Count(c => c.GetArguments().FirstOrDefault() is GetConfigEntriesCommand);

    private void VerifyReloads(string entryId, int times) =>
        _ctx.HaContext.Received(times).CallService("homeassistant", "reload_config_entry", Arg.Any<ServiceTarget?>(),
            Arg.Is<object?>(d => d != null && JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).Contains($"\"entry_id\":\"{entryId}\"")));

    private static ConfigEntry Failing(string entryId, string reason = "Connection refused") => new()
    {
        EntryId = entryId,
        Domain = entryId,
        Title = char.ToUpperInvariant(entryId[0]) + entryId[1..],
        State = "setup_retry",
        Reason = reason
    };

    private static ConfigEntry Loaded(string entryId) => Failing(entryId) with { State = "loaded", Reason = null };
}
