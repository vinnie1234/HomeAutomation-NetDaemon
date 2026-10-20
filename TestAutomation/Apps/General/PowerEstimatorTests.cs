using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class PowerEstimatorTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IEntityManager _entityManager = Substitute.For<IEntityManager>();

    private static readonly EstimatedPowerDevice Washer = new()
    {
        Name = "Wasmachine", EnergySensor = "sensor.washer_energy", StatusEntity = "sensor.washer_status",
        RunningStates = ["active"], PowerSensor = "sensor.washer_power"
    };

    private static readonly EstimatedPowerDevice Dryer = new()
    {
        Name = "Droger", EnergySensor = "sensor.dryer_energy", StatusEntity = "binary_sensor.dryer_running",
        RunningStates = ["on"], PowerSensor = "sensor.dryer_power"
    };

    public PowerEstimatorTests()
    {
        _entityManager.EntityExists(Arg.Any<string>()).Returns(true);
        SetState(Washer.StatusEntity, "idle");
        SetState(Washer.EnergySensor, "692.8");
        SetState(Dryer.StatusEntity, "off");
        SetState(Dryer.EnergySensor, "10.0");

        var configuration = new AppConfiguration { PowerEstimation = new PowerEstimationConfiguration { Devices = [Washer, Dryer] } };
        _ctx.InitApp<PowerEstimator>(Options.Create(new AppConfig()), Options.Create(configuration), _entityManager);
    }

    private void SetState(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    [Fact]
    public void EnergyStep_WhileRunning_PublishesTheAveragePowerSinceThePreviousStep()
    {
        _ctx.ChangeStateFor(Washer.StatusEntity).FromState("idle").ToState("active");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(30).Ticks);

        _ctx.ChangeStateFor(Washer.EnergySensor).FromState("692.8").ToState("692.9");

        // 0.1 kWh in half an hour is 200 W
        _entityManager.Received(1).SetState(Washer.PowerSensor, 200d);
    }

    [Fact]
    public void NextStep_UsesTheTimeSinceTheLastStep()
    {
        _ctx.ChangeStateFor(Washer.StatusEntity).FromState("idle").ToState("active");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(30).Ticks);
        _ctx.ChangeStateFor(Washer.EnergySensor).FromState("692.8").ToState("692.9");
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(15).Ticks);

        _ctx.ChangeStateFor(Washer.EnergySensor).FromState("692.9").ToState("693.0");

        _entityManager.Received(1).SetState(Washer.PowerSensor, 400d);
    }

    [Fact]
    public void Stopping_SetsThePowerToZero()
    {
        _ctx.ChangeStateFor(Washer.StatusEntity).FromState("idle").ToState("active");
        _entityManager.ClearReceivedCalls();

        _ctx.ChangeStateFor(Washer.StatusEntity).FromState("active").ToState("pause");

        _entityManager.Received(1).SetState(Washer.PowerSensor, 0);
    }

    [Fact]
    public void EnergyStep_WhileNotRunning_IsIgnored()
    {
        _entityManager.ClearReceivedCalls();

        _ctx.ChangeStateFor(Washer.EnergySensor).FromState("692.8").ToState("692.9");

        _entityManager.DidNotReceiveWithAnyArgs().SetState(default!, default!);
    }

    [Fact]
    public void EveryDevice_IsEstimatedSeparately()
    {
        _ctx.ChangeStateFor(Washer.StatusEntity).FromState("idle").ToState("active");
        _ctx.ChangeStateFor(Dryer.StatusEntity).FromState("off").ToState("on");
        _ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks);

        _ctx.ChangeStateFor(Dryer.EnergySensor).FromState("10.0").ToState("11.5");

        _entityManager.Received(1).SetState(Dryer.PowerSensor, 1500d);
        _entityManager.DidNotReceive().SetState(Washer.PowerSensor, Arg.Is<object>(x => !Equals(x, 0)));
    }
}
