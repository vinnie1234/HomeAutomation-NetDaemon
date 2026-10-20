using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class WasherPowerEstimatorTests
{
    private const string Status = "sensor.badkamer_wasmachine_machinestatus";
    private const string Energy = "sensor.badkamer_wasmachine_energie";

    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();

    public WasherPowerEstimatorTests()
    {
        _entityManager.EntityExists(WasherPowerEstimator.SensorId).Returns(true);
        _ctx.HaContext.GetState(Status).Returns(new EntityState { EntityId = Status, State = "idle" });
        _ctx.HaContext.GetState(Energy).Returns(new EntityState { EntityId = Energy, State = "692.8" });
        _ctx.InitApp<WasherPowerEstimator>(Options.Create(new AppConfig()), _entityManager);
    }

    private void StartWash()
    {
        _ctx.ChangeStateFor(Status).FromState("idle").ToState("active");
    }

    [Fact]
    public void EnergyStep_WhileWashing_PublishesTheAveragePowerSinceThePreviousStep()
    {
        StartWash();
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(30).Ticks);

        _ctx.ChangeStateFor(Energy).FromState("692.8").ToState("692.9");

        // 0.1 kWh in half an hour is 200 W
        _entityManager.Received(1).SetState(WasherPowerEstimator.SensorId, 200d);
    }

    [Fact]
    public void NextStep_UsesTheTimeSinceTheLastStep()
    {
        StartWash();
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(30).Ticks);
        _ctx.ChangeStateFor(Energy).FromState("692.8").ToState("692.9");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);

        _ctx.ChangeStateFor(Energy).FromState("692.9").ToState("693.0");

        _entityManager.Received(1).SetState(WasherPowerEstimator.SensorId, 400d);
    }

    [Fact]
    public void WashDone_SetsThePowerToZero()
    {
        StartWash();
        _entityManager.ClearReceivedCalls();

        _ctx.ChangeStateFor(Status).FromState("active").ToState("idle");

        _entityManager.Received(1).SetState(WasherPowerEstimator.SensorId, 0);
    }

    [Fact]
    public void EnergyStep_WhileNotWashing_IsIgnored()
    {
        _entityManager.ClearReceivedCalls();

        _ctx.ChangeStateFor(Energy).FromState("692.8").ToState("692.9");

        _entityManager.DidNotReceiveWithAnyArgs().SetState(default!, default!);
    }
}
