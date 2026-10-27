using Automation.Helpers;
using Automation.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace TestAutomation.Helpers;

public class PetSnowyCleaningTests
{
    private static DateTimeOffset Dutch(int day, int hour, int minute) =>
        new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(1));

    [Theory]
    [InlineData(26, 12, 30, 27, 0, 4)]   // cleaned yesterday noon, we go to bed just after midnight: still the same day
    [InlineData(26, 12, 30, 27, 5, 59)]  // until six in the morning
    [InlineData(27, 1, 0, 26, 22, 0)]    // a cleaning at 01:00 belongs to the day before
    public void SameDay(int day1, int hour1, int minute1, int day2, int hour2, int minute2) =>
        PetSnowyCleaning.DayKey(Dutch(day1, hour1, minute1)).Should().Be(PetSnowyCleaning.DayKey(Dutch(day2, hour2, minute2)));

    [Theory]
    [InlineData(26, 12, 30, 27, 6, 0)]   // from six o'clock it is a new day
    [InlineData(26, 12, 30, 27, 22, 0)]  // and at 22:00 the cleaning of yesterday does not count
    public void DifferentDay(int day1, int hour1, int minute1, int day2, int hour2, int minute2) =>
        PetSnowyCleaning.DayKey(Dutch(day1, hour1, minute1)).Should().NotBe(PetSnowyCleaning.DayKey(Dutch(day2, hour2, minute2)));

    [Fact]
    public void MarkCleaned_ThenIsCleanedToday_OnTheSameDayOnly()
    {
        string? saved = null;
        var storage = Substitute.For<IDataRepository>();
        storage.When(s => s.Save(PetSnowyCleaning.LastCleanedDateKey, Arg.Any<string>())).Do(c => saved = c.ArgAt<string>(1));
        storage.Get<string>(PetSnowyCleaning.LastCleanedDateKey).Returns(_ => saved);

        PetSnowyCleaning.IsCleanedToday(storage, Dutch(26, 13, 0)).Should().BeFalse();
        PetSnowyCleaning.MarkCleaned(storage, Dutch(26, 12, 30));

        PetSnowyCleaning.IsCleanedToday(storage, Dutch(26, 22, 0)).Should().BeTrue();
        PetSnowyCleaning.IsCleanedToday(storage, Dutch(27, 0, 4)).Should().BeTrue();
        PetSnowyCleaning.IsCleanedToday(storage, Dutch(27, 22, 0)).Should().BeFalse();
    }
}
