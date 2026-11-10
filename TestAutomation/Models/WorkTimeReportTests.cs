using Automation.Models.Work;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Models;

public class WorkTimeReportTests
{
    private static WorkDay Day(double homeHours = 0, double officeHours = 0, double otherHours = 0) =>
        new() { Home = homeHours * 3600, Office = officeHours * 3600, Other = otherHours * 3600 };

    [Fact]
    public void ADayOfRealWork_HasTheLunchBreakAndTheSmokingBreaksTakenOff()
    {
        var totals = WorkTimeReport.Of(Day(homeHours: 8));

        totals.Gross.Should().Be(480);
        totals.WithBreak.Should().Be(450);
        totals.Net.Should().Be(430);
    }

    [Fact]
    public void AShortDayBeforeLunch_HasNoLunchTakenOff_ButAShareOfTheSmokingBreaks()
    {
        var day = Day(homeHours: 2);
        day.First = "08:24";
        day.Last = "10:30";

        var totals = WorkTimeReport.Of(day);

        totals.Gross.Should().Be(120);
        totals.WithBreak.Should().Be(120);
        totals.Net.Should().Be(110, "10 of the 20 minutes: half of the four hours");
    }

    [Fact]
    public void ADayThatHasGonePastLunchTime_HasTheLunchTakenOff_EvenUnderFourHours()
    {
        var day = Day(homeHours: 3.5);
        day.First = "08:30";
        day.Last = "13:00";

        var totals = WorkTimeReport.Of(day);

        totals.WithBreak.Should().Be(210 - 30);
        totals.Net.Should().Be(180 - 18);
    }

    [Fact]
    public void AnEveningAfterLunchTime_HasNoLunchTakenOff()
    {
        var day = Day(officeHours: 0.5);
        day.First = "17:10";
        day.Last = "17:40";

        WorkTimeReport.Of(day).WithBreak.Should().Be(30);
    }

    [Fact]
    public void HoursAddedByHand_ComeOnTopOfTheDay_WithoutBreaksTakenOff()
    {
        var day = Day(homeHours: 8);
        day.ManualHome = 2 * 3600;
        day.ManualOffice = 30 * 60;

        var totals = WorkTimeReport.Of(day);

        totals.Manual.Should().Be(150);
        totals.Gross.Should().Be(480 + 150);
        totals.WithBreak.Should().Be(450 + 150);
        totals.Net.Should().Be(430 + 150);
        totals.Home.Should().Be(480 + 120);
        totals.Office.Should().Be(30);
    }

    [Fact]
    public void ADayWithOnlyHoursAddedByHand_HasNoBreakAtAll()
    {
        var day = new WorkDay { ManualOther = 3 * 3600 };

        var totals = WorkTimeReport.Of(day);

        totals.Gross.Should().Be(180);
        totals.Net.Should().Be(180);
        totals.Other.Should().Be(180);
    }

    [Fact]
    public void MinutesTakenOffByHand_NeverMakeADayNegative()
    {
        var day = Day(homeHours: 1);
        day.ManualHome = -2 * 3600;

        var totals = WorkTimeReport.Of(day);

        totals.Gross.Should().Be(0);
        totals.Net.Should().Be(0);
        totals.Manual.Should().Be(-120);
    }

    [Fact]
    public void WeeksAndMonths_CountTheHoursAddedByHand()
    {
        var monday = Day(homeHours: 8);
        monday.ManualHome = 60 * 60;
        var days = new Dictionary<string, WorkDay> { ["2026-11-09"] = monday, ["2026-11-10"] = new WorkDay { ManualOffice = 2 * 3600 } };

        var week = WorkTimeReport.Weeks(days, 1).Single();

        week.Totals.Manual.Should().Be(180);
        week.Totals.Gross.Should().Be(480 + 60 + 120);
    }

    [Fact]
    public void TheHoursAreDividedOverTheUsualPlaces_AtOfficeAndAtHome()
    {
        var totals = WorkTimeReport.Of(Day(homeHours: 1.5, officeHours: 6.5));

        totals.Home.Should().Be(90);
        totals.Office.Should().Be(390);
        totals.Other.Should().Be(0);
        totals.Gross.Should().Be(480);
    }

    [Fact]
    public void TheWeeksAreMondayToSunday_AndTheMonthsAreCalendarMonths_NotTheLast30Days()
    {
        var days = new Dictionary<string, WorkDay>
        {
            ["2026-10-30"] = Day(officeHours: 8),   // Friday of week 44, October
            ["2026-11-01"] = Day(homeHours: 5),     // Sunday of week 44, November
            ["2026-11-02"] = Day(homeHours: 8),     // Monday of week 45
            ["2026-11-10"] = Day(officeHours: 8)    // Tuesday of week 46
        };

        var weeks = WorkTimeReport.Weeks(days, 10);
        var months = WorkTimeReport.Months(days, 10);

        weeks.Select(w => w.Key).Should().Equal("2026-W46", "2026-W45", "2026-W44");
        weeks[2].From.Should().Be("2026-10-26");
        weeks[2].To.Should().Be("2026-11-01");
        weeks[2].Totals.Gross.Should().Be(480 + 300);
        months.Select(m => m.Key).Should().Equal("2026-11", "2026-10");
        months[0].Totals.Gross.Should().Be(300 + 480 + 480);
        months[0].From.Should().Be("2026-11-01");
        months[0].To.Should().Be("2026-11-30");
        months[1].Totals.Gross.Should().Be(480);
    }

    [Fact]
    public void ABreakIsTakenOffPerDay_NotOnceOverAWeek()
    {
        var days = new Dictionary<string, WorkDay>
        {
            ["2026-11-09"] = Day(homeHours: 8),
            ["2026-11-10"] = Day(homeHours: 8)
        };

        var week = WorkTimeReport.Weeks(days, 1).Single();

        week.Totals.Gross.Should().Be(960);
        week.Totals.WithBreak.Should().Be(900);
        week.Totals.Net.Should().Be(860);
    }

    [Fact]
    public void TheNewestDaysComeFirst_AndOnlyAsManyAsAsked()
    {
        var days = new Dictionary<string, WorkDay>
        {
            ["2026-11-08"] = Day(homeHours: 1), ["2026-11-10"] = Day(homeHours: 2), ["2026-11-09"] = Day(homeHours: 3)
        };

        WorkTimeReport.Days(days, 2).Select(d => d.Key).Should().Equal("2026-11-10", "2026-11-09");
    }
}
