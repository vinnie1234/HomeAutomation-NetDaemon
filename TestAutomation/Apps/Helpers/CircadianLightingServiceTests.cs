using FluentAssertions;
using HomeAssistantGenerated;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Helpers;

public class CircadianLightingServiceTests
{
    private static AppTestContext ArrangeWithSunElevation(double? elevation)
    {
        var ctx = AppTestContext.New();
        ctx.WithEntityState("sun.sun", elevation is >= 0 ? "above_horizon" : "below_horizon");

        if (elevation != null)
            ctx.SetAttributesFor("sun.sun", new { elevation });

        return ctx;
    }

    /// <summary>
    /// Arranges the sun entity the way Home Assistant reports it: the <c>next_*</c> timestamps
    /// always point into the future, so the solar day around "now" has to be reconstructed.
    /// </summary>
    /// <param name="noonRelativeToNow">Where solar noon sits relative to now (negative = already passed).</param>
    /// <param name="dawnBeforeNoon">How long before solar noon dawn is.</param>
    /// <param name="duskAfterNoon">How long after solar noon dusk is.</param>
    /// <param name="elevation">Optional elevation, only used when the timestamps are unusable.</param>
    private static AppTestContext ArrangeWithSolarDay(
        TimeSpan noonRelativeToNow,
        TimeSpan dawnBeforeNoon,
        TimeSpan duskAfterNoon,
        double? elevation = null,
        string? brokenNoon = null)
    {
        var now = DateTimeOffset.Now;
        var noon = now + noonRelativeToNow;

        var ctx = AppTestContext.New();
        ctx.WithEntityState("sun.sun", "above_horizon");
        ctx.SetAttributesFor("sun.sun", new
        {
            elevation,
            next_dawn = NextOccurrence(noon - dawnBeforeNoon, now),
            next_noon = brokenNoon ?? NextOccurrence(noon, now),
            next_dusk = NextOccurrence(noon + duskAfterNoon, now)
        });

        return ctx;
    }

    private static string NextOccurrence(DateTimeOffset timestamp, DateTimeOffset now)
    {
        while (timestamp <= now) timestamp = timestamp.AddDays(1);
        return timestamp.ToString("O");
    }

    [Theory]
    // Sun high in the sky: cool daylight white.
    [InlineData(45.0, 6000)]
    [InlineData(20.0, 6000)]
    // Halfway between the night (-6) and day (20) threshold: halfway between 2500K and 6000K.
    [InlineData(7.0, 4250)]
    // Golden hour: clearly warmer than midday, but not the warmest yet.
    [InlineData(3.0, 3700)]
    // Sunset and below: warmest setting.
    [InlineData(-6.0, 2500)]
    [InlineData(-20.0, 2500)]
    public void GetColorTemperature_FollowsTheSun(double elevation, long expectedKelvin)
    {
        var ctx = ArrangeWithSunElevation(elevation);

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(expectedKelvin);
    }

    [Theory]
    [InlineData(45.0, 100)]
    [InlineData(7.0, 75)]
    [InlineData(-6.0, 50)]
    [InlineData(-20.0, 50)]
    public void GetBrightness_FollowsTheSun(double elevation, int expectedBrightness)
    {
        var ctx = ArrangeWithSunElevation(elevation);

        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(expectedBrightness);
    }

    [Fact]
    public void GetColorTemperature_NeverGetsCoolerWhenTheSunDrops()
    {
        long? previous = null;

        foreach (var elevation in new[] { 60.0, 30.0, 20.0, 10.0, 0.0, -6.0, -18.0 })
        {
            var kelvin = ArrangeWithSunElevation(elevation).CircadianLightingService.GetColorTemperature();

            if (previous != null) kelvin.Should().BeLessThanOrEqualTo(previous.Value);
            previous = kelvin;
        }
    }

    [Fact]
    public void GetBrightness_IsVeryDim_WhenSomebodyIsSleeping()
    {
        var ctx = ArrangeWithSunElevation(45.0);

        ctx.CircadianLightingService.GetBrightness(isNightMode: true).Should().Be(5);
    }

    [Fact]
    public void GetColorTemperature_FallsBackToDaylight_WhenSunIsUnavailable()
    {
        var ctx = AppTestContext.New();

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(6000);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(100);
    }

    [Fact]
    public void GetColorTemperature_IsCoolest_AtSolarNoon()
    {
        var ctx = ArrangeWithSolarDay(TimeSpan.Zero, TimeSpan.FromHours(7), TimeSpan.FromHours(7));

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(6000);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(100);
    }

    [Fact]
    public void GetColorTemperature_IsWarmest_AtDawn()
    {
        // Now is exactly dawn: solar noon is still 7 hours away.
        var ctx = ArrangeWithSolarDay(TimeSpan.FromHours(7), TimeSpan.FromHours(7), TimeSpan.FromHours(7));

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(2500);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(50);
    }

    [Fact]
    public void GetColorTemperature_IsHalfway_MidMorning()
    {
        // Dawn was 4 hours ago, solar noon is 4 hours away.
        var ctx = ArrangeWithSolarDay(TimeSpan.FromHours(4), TimeSpan.FromHours(8), TimeSpan.FromHours(8));

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(4250);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(75);
    }

    [Fact]
    public void GetColorTemperature_IsHalfway_MidAfternoon()
    {
        // Solar noon was 4 hours ago, dusk is 4 hours away.
        var ctx = ArrangeWithSolarDay(TimeSpan.FromHours(-4), TimeSpan.FromHours(8), TimeSpan.FromHours(8));

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(4250);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(75);
    }

    [Fact]
    public void GetColorTemperature_IsWarmest_AfterDusk()
    {
        // Solar noon was 10 hours ago and dusk 2 hours ago: late evening.
        var ctx = ArrangeWithSolarDay(TimeSpan.FromHours(-10), TimeSpan.FromHours(8), TimeSpan.FromHours(8));

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(2500);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(50);
    }

    [Fact]
    public void GetColorTemperature_ReachesFullDaylight_OnAShortWinterDay()
    {
        // Around the winter solstice the sun peaks at roughly 14 degrees here, well below the
        // configured day elevation of 20. The timestamps still put us at solar noon, so the
        // lights must reach full daylight instead of staying stuck near their night values.
        var ctx = ArrangeWithSolarDay(TimeSpan.Zero, TimeSpan.FromHours(4), TimeSpan.FromHours(4),
            elevation: 14.0);

        ctx.CircadianLightingService.GetColorTemperature().Should().Be(6000);
        ctx.CircadianLightingService.GetBrightness(isNightMode: false).Should().Be(100);
    }

    [Fact]
    public void GetColorTemperature_FallsBackToElevation_WhenSunTimestampsAreUnusable()
    {
        var ctx = ArrangeWithSolarDay(TimeSpan.Zero, TimeSpan.FromHours(7), TimeSpan.FromHours(7),
            elevation: 7.0, brokenNoon: "unknown");

        // Halfway between the night (-6) and day (20) elevation threshold.
        ctx.CircadianLightingService.GetColorTemperature().Should().Be(4250);
    }

    [Fact]
    public void GetColorTemperature_ForLight_IsSkipped_WhenLightHasNoColorTempMode()
    {
        var ctx = ArrangeWithSunElevation(45.0);
        var light = ctx.GetEntity<LightEntity>("light.gang_sigaret", "on")!;
        ctx.SetAttributesFor("light.gang_sigaret", new { supported_color_modes = new[] { "hs", "xy" } });

        ctx.CircadianLightingService.GetColorTemperature(light).Should().BeNull();
    }

    [Fact]
    public void GetColorTemperature_ForLight_IsClampedToWhatTheLightSupports()
    {
        var ctx = ArrangeWithSunElevation(45.0);
        var light = ctx.GetEntity<LightEntity>("light.hal_2", "on")!;
        ctx.SetAttributesFor("light.hal_2", new
        {
            supported_color_modes = new[] { "color_temp" },
            min_color_temp_kelvin = 2202,
            max_color_temp_kelvin = 4000
        });

        // Would be 6000K at this sun elevation, but the lamp only reaches 4000K.
        ctx.CircadianLightingService.GetColorTemperature(light).Should().Be(4000);
    }

    [Fact]
    public void GetColorTemperature_ForLight_IsUsed_WhenLightSupportsColorTemp()
    {
        var ctx = ArrangeWithSunElevation(-20.0);
        var light = ctx.GetEntity<LightEntity>("light.hal_2", "on")!;
        ctx.SetAttributesFor("light.hal_2", new
        {
            supported_color_modes = new[] { "color_temp", "xy" },
            min_color_temp_kelvin = 2000,
            max_color_temp_kelvin = 6535
        });

        ctx.CircadianLightingService.GetColorTemperature(light).Should().Be(2500);
    }

    [Fact]
    public void GetColorTemperature_ForLight_IsUsed_WhenLightReportsNoAttributes()
    {
        var ctx = ArrangeWithSunElevation(45.0);
        var light = ctx.GetEntity<LightEntity>("light.hal_2")!;

        ctx.CircadianLightingService.GetColorTemperature(light).Should().Be(6000);
    }
}
