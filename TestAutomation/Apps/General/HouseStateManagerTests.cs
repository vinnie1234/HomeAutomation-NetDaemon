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

    [Fact]
    public void WorkFromTheCalendar_StartsWhenTheHoursOfTheCalendarStart_NotAtTheUsualWorkStart()
    {
        // Monday 3 January 2000: her usual start is 08:30, the calendar says 10:00 - 19:30 (to be at the office from 09:50, no margin after)
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        void Set(string id, string state) => ctx.HaContext.GetState(id).Returns(new EntityState { EntityId = id, State = state });
        Set("input_boolean.onvacation", "off");
        Set("input_boolean.vrij_carleen", "off");
        Set("input_select.werkdag_carleen_maandag", "Kantoor");
        Set("input_datetime.werk_start_carleen", "08:30:00");
        Set("input_boolean.werk_uit_agenda_carleen", "on");
        Set("input_datetime.kantoor_aankomst_carleen", "2000-01-03 09:50:00");
        Set("input_datetime.kantoor_vertrek_carleen", "2000-01-03 19:30:00");
        Set("input_number.reis_marge_voor_carleen", "10.0");
        Set("input_number.reis_marge_na_carleen", "0.0");
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 0, 0, 0, TimeSpan.Zero).UtcTicks);
        ctx.InitApp<HouseStateManager>();

        // 08:45 Dutch time: the usual start has passed, the calendar hours have not started
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 7, 45, 0, TimeSpan.Zero).UtcTicks);
        ctx.VerifyCallService("input_boolean", "turn_on", "workingcarleen", times: 0);

        // 10:01 Dutch time (the mocked state never turns on, so it asks again each minute of the window)
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 9, 1, 0, TimeSpan.Zero).UtcTicks);
        var turnedOn = ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService"
            && (string)c.GetArguments()[0]! == "input_boolean" && (string)c.GetArguments()[1]! == "turn_on"
            && ((ServiceTarget?)c.GetArguments()[2])?.EntityIds?.Contains("input_boolean.workingcarleen") == true);
        Assert.True(turnedOn >= 1);
    }

    [Fact]
    public void WorkFromTheCalendar_StopsAtTheEnd_AlsoWhenTheEntitiesHaveMovedOnToTheNextDay()
    {
        // Monday 3 January 2000: working 10:00 - 19:30; at 19:30:30 the office times move on to next Monday, before the next check
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        void Set(string id, string state) => ctx.HaContext.GetState(id).Returns(new EntityState { EntityId = id, State = state });
        Set("input_boolean.onvacation", "off");
        Set("input_boolean.vrij_carleen", "off");
        Set("input_select.werkdag_carleen_maandag", "Kantoor");
        Set("input_boolean.werk_uit_agenda_carleen", "on");
        Set("input_datetime.kantoor_aankomst_carleen", "2000-01-03 09:50:00");
        Set("input_datetime.kantoor_vertrek_carleen", "2000-01-03 19:30:00");
        Set("input_number.reis_marge_voor_carleen", "10.0");
        Set("input_number.reis_marge_na_carleen", "0.0");
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 0, 0, 0, TimeSpan.Zero).UtcTicks);
        ctx.InitApp<HouseStateManager>();
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 17, 0, 0, TimeSpan.Zero).UtcTicks);   // 18:00 Dutch time, in the hours
        Set("input_boolean.workingcarleen", "on");
        ctx.HaContext.ClearReceivedCalls();

        // The entities move on to the next Monday just after 19:30 Dutch time
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 18, 29, 0, TimeSpan.Zero).UtcTicks);
        Set("input_datetime.kantoor_aankomst_carleen", "2000-01-10 08:20:00");
        Set("input_datetime.kantoor_vertrek_carleen", "2000-01-10 17:00:00");
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 18, 33, 0, TimeSpan.Zero).UtcTicks);

        var turnedOff = ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService"
            && (string)c.GetArguments()[0]! == "input_boolean" && (string)c.GetArguments()[1]! == "turn_off"
            && ((ServiceTarget?)c.GetArguments()[2])?.EntityIds?.Contains("input_boolean.workingcarleen") == true);
        Assert.True(turnedOff >= 1);
    }

    private static int Calls(AppTestContext ctx, string service, string entityId) =>
        ctx.HaContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "CallService"
            && (string)c.GetArguments()[0]! == "input_boolean" && (string)c.GetArguments()[1]! == service
            && ((ServiceTarget?)c.GetArguments()[2])?.EntityIds?.Contains(entityId) == true);

    private AppTestContext ArriveAtWorkAndLeave(string workingAfter)
    {
        // Monday 3 January 2000, Carleen at the office: at work at 12:00 Dutch time, away from 12:30
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        void Set(string id, string state, object? attributes = null) => ctx.HaContext.GetState(id).Returns(new EntityState
            { EntityId = id, State = state, AttributesJson = attributes == null ? null : System.Text.Json.JsonSerializer.SerializeToElement(attributes) });
        Set("input_boolean.onvacation", "off");
        Set("input_boolean.vrij_carleen", "off");
        Set("input_select.werkdag_carleen_maandag", "Kantoor");
        Set("input_boolean.workingcarleen", workingAfter);
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 8, 0, 0, TimeSpan.Zero).UtcTicks);
        ctx.InitApp<HouseStateManager>();
        Set("zone.werk_carleen", "1", new { persons = new[] { "person.carleen" } });
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 11, 30, 0, TimeSpan.Zero).UtcTicks);
        Set("zone.werk_carleen", "0", new { persons = Array.Empty<string>() });
        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 12, 20, 0, TimeSpan.Zero).UtcTicks);   // 13:20: away for 50 minutes
        ctx.HaContext.ClearReceivedCalls();
        return ctx;
    }

    [Fact]
    public void AwayFromWork_ForMoreThanAnHour_OnAnOfficeDay_StopsWorking()
    {
        var ctx = ArriveAtWorkAndLeave("on");

        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 12, 45, 0, TimeSpan.Zero).UtcTicks);   // 13:45: more than an hour after the last time at work (12:30)

        Assert.True(Calls(ctx, "turn_off", "input_boolean.workingcarleen") >= 1);
    }

    [Fact]
    public void AwayFromWork_ForLessThanAnHour_KeepsWorking()
    {
        var ctx = ArriveAtWorkAndLeave("on");

        Assert.Equal(0, Calls(ctx, "turn_off", "input_boolean.workingcarleen"));
    }

    [Fact]
    public void ComingBackToWork_BeforeTheEnd_StartsWorkingAgain()
    {
        var ctx = ArriveAtWorkAndLeave("off");
        ctx.HaContext.GetState("zone.werk_carleen").Returns(new EntityState
        {
            EntityId = "zone.werk_carleen", State = "1",
            AttributesJson = System.Text.Json.JsonSerializer.SerializeToElement(new { persons = new[] { "person.carleen" } })
        });

        ctx.AdvanceTimeTo(new DateTimeOffset(2000, 1, 3, 12, 30, 0, TimeSpan.Zero).UtcTicks);

        Assert.True(Calls(ctx, "turn_on", "input_boolean.workingcarleen") >= 1);
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
