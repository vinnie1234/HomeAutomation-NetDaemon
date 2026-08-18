using Automation.apps.General;
using FluentAssertions;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class SleepManagerTests
{
    private AppTestContext SetupContext()
    {
        var ctx = AppTestContext.New();

        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.disablelightautomationgeneral").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.datenight").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.onvacation").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.douchen").Returns(new EntityState { State = "off" });
        
        ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { State = "Geen" });
        ctx.HaContext.GetState("sensor.afval_vandaag").Returns(new EntityState { State = "Geen" });
        ctx.HaContext.GetState("sensor.petsnowy_litterbox_errors").Returns(new EntityState { State = "0" });
        ctx.HaContext.GetState("cover.rollerblind_0003").Returns(new EntityState { State = "closed" });
        ctx.HaContext.GetAllEntities().Returns(new List<Entity>());

        return ctx;
    }

    [Fact]
    public void VincentSleeping_TurnsOnCarleenSleeping_WhenCarleenIsHome()
    {
        var ctx = SetupContext();
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyCallService("input_boolean", "turn_on", "sleepingcarleen", times: 1);
    }

    [Fact]
    public void VincentSleeping_DoesNotTurnOnCarleenSleeping_WhenCarleenIsAway()
    {
        var ctx = SetupContext();
        // Set Carleen to away
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "on" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyNotCallService("input_boolean.turn_on", "sleepingcarleen");
    }

    [Fact]
    public void VincentSleeping_TurnsOffTV()
    {
        var ctx = SetupContext();
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");

        
        TestExceptionCatcher.CaughtException.Should().BeNull("because Sleeping() should not throw: " + TestExceptionCatcher.CaughtException?.ToString());

        ctx.VerifyCallService("media_player", "turn_off", "tv", times: 1);
    }

    [Fact]
    public void VincentSleeping_ClosesRollerblind()
    {
        var ctx = SetupContext();
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyCallService("cover", "set_cover_position", "rollerblind_0003", times: 1);
    }

    [Fact]
    public void VincentSleeping_TurnsOffAllLights_WhenNotDisabled()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.disablelightautomationgeneral").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("light.dummy_light").Returns(new EntityState { EntityId = "light.dummy_light", State = "on" });
        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { new Entity(ctx.HaContext, "light.dummy_light") });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");

        
        TestExceptionCatcher.CaughtException.Should().BeNull("Because an exception was caught during SleepManager execution");

        // Testing light.turn_all_off or light.turn_off without entity_id? 
        ctx.VerifyCallService("light", "turn_off", "dummy_light", times: 1);
    }

    [Fact]
    public void VincentSleeping_DoesNotTurnOffLights_WhenDisabled()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.disablelightautomationgeneral").Returns(new EntityState { State = "on" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyNotCallService("light.turn_off");
    }

    [Fact]
    public void VincentSleeping_ClearsAwayStates()
    {
        var ctx = SetupContext();
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyCallService("input_boolean", "turn_off", "awayvincent", times: 1);
        ctx.VerifyCallService("input_boolean", "turn_off", "awaycarleen", times: 1);
        ctx.VerifyCallService("input_boolean", "turn_off", "douchen", times: 1);
    }

    [Fact]
    public void VincentSleeping_NotifiesGarbage_WhenNotNone()
    {
        var ctx = SetupContext();
        ctx.SetCurrentTime(new DateTime(2023, 1, 1, 22, 0, 0)); // Not between 0 and 7
        ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { State = "Plastic" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
    }

    [Fact]
    public void VincentSleeping_DoesNotNotifyGarbage_WhenNone()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { State = "Geen" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");


        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 0);
    }

    [Fact]
    public void VincentSleeping_NotifiesPetSnowyErrors_WhenErrorsExist()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("sensor.petsnowy_litterbox_errors").Returns(new EntityState { State = "2" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");

        TestExceptionCatcher.CaughtException.Should().BeNull("Because an exception was caught during SleepManager execution");

        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
    }

    [Fact]
    public void VincentWakeUp_OpensRollerblind_WhenCarleenNotHome()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "on" }); // Carleen not home
        ctx.SetCurrentTime(new DateTime(2023, 1, 1, 8, 0, 0)); // Ensure hour >= 7
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("on").ToState("off");


        ctx.VerifyCallService("cover", "set_cover_position", "rollerblind_0003", times: 1);
    }

    [Fact]
    public void VincentWakeUp_DoesNotOpenRollerblind_WhenCarleenHomeAndSleeping()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "on" });
        ctx.SetCurrentTime(new DateTime(2023, 1, 1, 8, 0, 0)); // Ensure hour >= 7
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("on").ToState("off");


        ctx.VerifyNotCallService("cover.set_cover_position");
    }

    [Fact]
    public void VincentWakeUp_GoesBackToSleep_WhenBefore7AM()
    {
        var ctx = SetupContext();
        ctx.SetCurrentTime(new DateTime(2023, 1, 1, 5, 0, 0)); // Before 7 AM
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("on").ToState("off");


        ctx.VerifyCallService("input_boolean", "turn_on", "sleepingvincent", times: 1);
    }

    [Fact]
    public void VincentWakeUp_SendsBatteryWarning_WhenPhoneLow()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("sensor.vincent_phone_battery_level").Returns(new EntityState { State = "20" });
        ctx.HaContext.GetState("binary_sensor.vincent_phone_is_charging").Returns(new EntityState { State = "off" });
        ctx.SetCurrentTime(new DateTime(2023, 1, 1, 8, 0, 0)); // Ensure hour >= 7
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("on").ToState("off");

        
        TestExceptionCatcher.CaughtException.Should().BeNull("Because an exception was caught during SleepManager execution");

        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
    }

    [Fact]
    public void VincentWakeUp_DoesNotSendBatteryWarning_WhenPhoneCharging()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("sensor.vincent_phone_battery_level").Returns(new EntityState { State = "20" });
        ctx.HaContext.GetState("binary_sensor.vincent_phone_is_charging").Returns(new EntityState { State = "on" }); // Charging
        ctx.SetCurrentTime(new DateTime(2023, 1, 1, 8, 0, 0)); // Ensure hour >= 7
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("on").ToState("off");


        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 0);
    }

    [Fact]
    public void CarleenWakeUp_OpensRollerblind_WhenVincentAlsoAwake()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "off" }); // Vincent is awake
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("on").ToState("off");


        ctx.VerifyCallService("cover", "set_cover_position", "rollerblind_0003", times: 1);
    }

    [Fact]
    public void CarleenWakeUp_DoesNotOpenRollerblind_WhenVincentSleeping()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "on" }); // Vincent is sleeping
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("on").ToState("off");


        ctx.VerifyNotCallService("cover.set_cover_position");
    }

    [Fact]
    public void CarleenWakeUp_DoesNothing_WhenNotHome()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { State = "on" }); // Carleen not home
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "off" }); 
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("on").ToState("off");


        ctx.VerifyNotCallService("cover.set_cover_position");
    }

    [Fact]
    public void TvTurnsOn_WakesBothUp_WhenBothSleeping()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "on" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "on" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("media_player.tv").FromState("off").ToState("on");


        ctx.VerifyCallService("input_boolean", "turn_off", "sleepingvincent", times: 1);
        ctx.VerifyCallService("input_boolean", "turn_off", "sleepingcarleen", times: 1);
    }

    [Fact]
    public void BureauLightTurnsOn_WakesOnlyVincent_WhenBothSleeping()
    {
        var ctx = SetupContext();
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { State = "on" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { State = "on" });
        ctx.InitApp<SleepManager>();

        ctx.ChangeStateFor("light.bureau").FromState("off").ToState("on");


        // The desk light is Vincent's: it may only wake him, never Carleen.
        ctx.VerifyCallService("input_boolean", "turn_off", "sleepingvincent", times: 1);
        ctx.VerifyCallService("input_boolean", "turn_off", "sleepingcarleen", times: 0);
    }
}

