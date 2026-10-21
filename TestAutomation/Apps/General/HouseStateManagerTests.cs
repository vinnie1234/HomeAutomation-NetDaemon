using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HouseStateManagerTests
{
    private void SetupDefaultStates(AppTestContext ctx)
    {
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "above_horizon" });
        ctx.HaContext.GetState("input_datetime.nighttimeweekdays").Returns(new EntityState { EntityId = "input_datetime.nighttimeweekdays", State = "00:00:00" });
        ctx.HaContext.GetState("input_datetime.nighttimeweekends").Returns(new EntityState { EntityId = "input_datetime.nighttimeweekends", State = "00:30:00" });
        ctx.HaContext.GetState("input_datetime.daytimeweekend").Returns(new EntityState { EntityId = "input_datetime.daytimeweekend", State = "10:00:00" });
        ctx.HaContext.GetState("input_datetime.opstaan_thuiswerk_vincent").Returns(new EntityState { EntityId = "input_datetime.opstaan_thuiswerk_vincent", State = "08:15:00" });
        ctx.HaContext.GetState("input_datetime.opstaan_kantoor_vincent").Returns(new EntityState { EntityId = "input_datetime.opstaan_kantoor_vincent", State = "07:15:00" });
        ctx.HaContext.GetState("input_boolean.vrij_vincent").Returns(new EntityState { EntityId = "input_boolean.vrij_vincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.working").Returns(new EntityState { EntityId = "input_boolean.working", State = "off" });
        ctx.HaContext.GetState("input_boolean.workingvincent").Returns(new EntityState { EntityId = "input_boolean.workingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.workingcarleen").Returns(new EntityState { EntityId = "input_boolean.workingcarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Day" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
    }

    [Fact]
    public void Constructor_SetsEveningState_WhenSunBelowHorizon()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void Constructor_SetsDayState_WhenSunAboveHorizon()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "above_horizon" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void Constructor_DoesNotSetState_WhenSunStateUnknown()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unknown" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void Constructor_DoesNotSetState_WhenSunStateUnavailable()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unavailable" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void SunGoesDown_SetsEveningState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("above_horizon").ToState("below_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void SunComesUp_SetsMorningState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Night" });
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("below_horizon").ToState("above_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Morning");
    }

    [Fact]
    public void SunComesUp_KeepsDay_WhenDayWasAlreadySet()
    {
        // Arrange - e.g. an office day in winter: Day is set at 07:15, sunrise is later
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });
        ctx.InitApp<HouseStateManager>();
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Day" });

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("below_horizon").ToState("above_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption_NotChangedTo("input_select.housemodeselect", "Morning");
    }

    [Fact]
    public void SunGoesDown_KeepsNight_WhenSomeoneAlreadySleeps()
    {
        // Arrange - e.g. going to bed before sunset in summer
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Night" });

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("above_horizon").ToState("below_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption_NotChangedTo("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void DaytimeOffice_IsRescheduled_WhenChangedInHomeAssistant()
    {
        // Arrange - Monday 3 January 2000 (an office day), the office daytime is moved from 07:15 to 06:00
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_select.werkdag_vincent_maandag").Returns(new EntityState { EntityId = "input_select.werkdag_vincent_maandag", State = "Kantoor" });
        ctx.HaContext.GetState("input_boolean.onvacation").Returns(new EntityState { EntityId = "input_boolean.onvacation", State = "off" });
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 0, 0, 0, TimeSpan.Zero).UtcTicks); // Monday 01:00 Dutch time
        ctx.InitApp<HouseStateManager>();
        ctx.ChangeStateFor("input_datetime.opstaan_kantoor_vincent").FromState("07:15:00").ToState("06:00:00");
        ctx.HaContextMock.ProcessPendingOperations();
        ctx.HaContext.ClearReceivedCalls();

        // Act - advance to 06:30 Dutch time (05:30 UTC) on Monday, before the old 07:15
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 5, 30, 0, TimeSpan.Zero).UtcTicks);

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void SunStateChange_IgnoredFromUnknown()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unknown" });
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("unknown").ToState("above_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void SunStateChange_IgnoredFromUnavailable()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unavailable" });
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("unavailable").ToState("below_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void SceneActivation_SetsDay_WhenWoonkamerdayActivated()
    {
        // Arrange — use below_horizon so constructor sets Evening first, not Day
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });
        ctx.InitApp<HouseStateManager>();
        ctx.HaContext.ClearReceivedCalls();

        // Act
        ctx.ChangeStateFor("scene.woonkamerday").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void SceneActivation_SetsEvening_WhenWoonkamereveningActivated()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("scene.woonkamerevening").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void SceneActivation_SetsNight_WhenWoonkamernightActivated()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("scene.woonkamernight").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Fact]
    public void SceneActivation_SetsMorning_WhenWoonkamermorningActivated()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("scene.woonkamermorning").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Morning");
    }

    [Fact]
    public void SleepingVincentTurnsOn_SetsNightState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Fact]
    public void SleepingCarleenTurnsOn_SetsNightState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Theory]
    [InlineData("input_boolean.workingvincent")]
    [InlineData("input_boolean.workingcarleen")]
    public void Working_TurnsOn_WhenOnePersonStartsWorking(string person)
    {
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        ctx.ChangeStateFor(person).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("input_boolean", "turn_on", "working");
    }

    [Fact]
    public void Working_StaysOn_WhileTheOtherPersonStillWorks()
    {
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.working").Returns(new EntityState { EntityId = "input_boolean.working", State = "on" });
        ctx.HaContext.GetState("input_boolean.workingcarleen").Returns(new EntityState { EntityId = "input_boolean.workingcarleen", State = "on" });
        ctx.InitApp<HouseStateManager>();

        ctx.ChangeStateFor("input_boolean.workingvincent").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("input_boolean", "turn_off", "working", times: 0);
    }

    [Fact]
    public void Working_TurnsOff_WhenNobodyWorksAnymore()
    {
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.working").Returns(new EntityState { EntityId = "input_boolean.working", State = "on" });
        ctx.InitApp<HouseStateManager>();

        ctx.ChangeStateFor("input_boolean.workingvincent").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.VerifyCallService("input_boolean", "turn_off", "working");
    }

    [Theory]
    [InlineData("Kantoor", 1)]
    [InlineData("Thuis", 1)]
    [InlineData("Vrij", 0)]
    public void CarleensWorkStart_TurnsHerWorkingOn_OnlyOnHerWorkDays(string monday, int expectedTurnOns)
    {
        // Arrange - Monday 3 January 2000, Carleen starts at 06:30 (her own working hours)
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.onvacation").Returns(new EntityState { EntityId = "input_boolean.onvacation", State = "off" });
        ctx.HaContext.GetState("input_boolean.vrij_carleen").Returns(new EntityState { EntityId = "input_boolean.vrij_carleen", State = "off" });
        ctx.HaContext.GetState("input_select.werkdag_carleen_maandag").Returns(new EntityState { EntityId = "input_select.werkdag_carleen_maandag", State = monday });
        ctx.HaContext.GetState("input_datetime.werk_start_carleen").Returns(new EntityState { EntityId = "input_datetime.werk_start_carleen", State = "06:30:00" });
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 0, 0, 0, TimeSpan.Zero).UtcTicks);
        ctx.InitApp<HouseStateManager>();

        // Act - 06:45 Dutch time
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 5, 45, 0, TimeSpan.Zero).UtcTicks);

        // Assert
        ctx.VerifyCallService("input_boolean", "turn_on", "workingcarleen", times: expectedTurnOns);
        ctx.VerifyCallService("input_boolean", "turn_on", "workingvincent", times: 0);
    }

    [Theory]
    [InlineData("Vrij", "Vrij", true)]
    [InlineData("Vrij", "Thuis", false)]
    public void WeekendDaytime_OnAWeekday_OnlyWhenBothHaveADayOff(string vincent, string carleen, bool expectDay)
    {
        // Arrange - Monday 3 January 2000; the weekend daytime is 10:00, the wake-up times for work are later than that
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("input_boolean.onvacation").Returns(new EntityState { EntityId = "input_boolean.onvacation", State = "off" });
        ctx.HaContext.GetState("input_select.werkdag_vincent_maandag").Returns(new EntityState { EntityId = "input_select.werkdag_vincent_maandag", State = vincent });
        ctx.HaContext.GetState("input_select.werkdag_carleen_maandag").Returns(new EntityState { EntityId = "input_select.werkdag_carleen_maandag", State = carleen });
        ctx.HaContext.GetState("input_datetime.opstaan_thuiswerk_carleen").Returns(new EntityState { EntityId = "input_datetime.opstaan_thuiswerk_carleen", State = "11:00:00" });
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 0, 0, 0, TimeSpan.Zero).UtcTicks);
        ctx.InitApp<HouseStateManager>();
        ctx.HaContext.ClearReceivedCalls();

        // Act - 10:15 Dutch time
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 9, 15, 0, TimeSpan.Zero).UtcTicks);

        // Assert
        if (expectDay) ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
        else ctx.VerifyInputSelect_SelectOption_NotChangedTo("input_select.housemodeselect", "Day");
    }
}
