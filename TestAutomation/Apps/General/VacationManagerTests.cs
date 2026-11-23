using Automation.apps.General.Presence;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class VacationManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.NewWithScheduler();

    private void State(string entityId, string state) =>
        _ctx.HaContext.GetState(entityId).Returns(new EntityState { EntityId = entityId, State = state });

    private void Start(string vincent, string carleen, string house = "off")
    {
        State("input_boolean.vakantie_vincent", vincent);
        State("input_boolean.vakantie_carleen", carleen);
        State("input_boolean.onvacation", house);
        _ctx.InitApp<VacationManager>();
    }

    private void Change(string entityId, string from, string to)
    {
        _ctx.ChangeStateFor(entityId).FromState(from).ToState(to);
        _ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void WhenOnlyOneIsOnVacation_TheHouseIsNot()
    {
        Start("off", "off");
        State("input_boolean.vakantie_vincent", "on");

        Change("input_boolean.vakantie_vincent", "off", "on");

        _ctx.VerifyCallService("input_boolean", "turn_on", "onvacation", times: 0);
    }

    [Fact]
    public void WhenBothAreOnVacation_TheHouseIsOnVacation()
    {
        Start("on", "off");
        State("input_boolean.vakantie_carleen", "on");

        Change("input_boolean.vakantie_carleen", "off", "on");

        _ctx.VerifyCallService("input_boolean", "turn_on", "onvacation", times: 1);
    }

    [Fact]
    public void WhenOneIsBack_TheHouseThatThisAppSwitchedOnGoesOff()
    {
        Start("on", "off");
        State("input_boolean.vakantie_carleen", "on");
        Change("input_boolean.vakantie_carleen", "off", "on");
        State("input_boolean.onvacation", "on");
        State("input_boolean.vakantie_carleen", "off");

        Change("input_boolean.vakantie_carleen", "on", "off");

        _ctx.VerifyCallService("input_boolean", "turn_off", "onvacation", times: 1);
    }

    [Fact]
    public void AVacationOfTheHouseFromElsewhere_StaysWhenOneIsBack()
    {
        Start("on", "on", house: "on");
        State("input_boolean.vakantie_carleen", "off");

        Change("input_boolean.vakantie_carleen", "on", "off");

        _ctx.VerifyCallService("input_boolean", "turn_off", "onvacation", times: 0);
    }
}
