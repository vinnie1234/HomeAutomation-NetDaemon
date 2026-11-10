using Automation.apps.General;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Apps.General;

public class GarbageOutsideTests
{
    [Theory]
    [InlineData("PMD", 8, 0, "Geen")]
    [InlineData("PMD", 16, 59, "Geen")]
    [InlineData("PMD", 17, 0, "PMD")]
    [InlineData("Papier", 23, 59, "Papier")]
    [InlineData("Geen", 20, 0, "Geen")]
    [InlineData("unknown", 20, 0, "Geen")]
    [InlineData(null, 20, 0, "Geen")]
    public void TheGarbageOfTomorrowCountsAsToPutOutsideOnlyFromTheEvening(string? tomorrow, int hour, int minute, string expected) =>
        GarbageOutside.Value(tomorrow, new TimeSpan(hour, minute, 0)).Should().Be(expected);
}
