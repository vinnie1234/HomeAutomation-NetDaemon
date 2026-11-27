using Automation.Helpers;
using FluentAssertions;
using Xunit;

namespace TestAutomation.Apps.General;

public class WindowAdviceTests
{
    /// <summary>A mild, dry afternoon outside and a house at 23° and 57% (11,7 g/m³).</summary>
    private static WindowConditions Mild(
        double? outdoorTemperature = 12, double? outdoorHumidity = 70, double? gust = 20, bool rain = false, bool warning = false, bool dark = false,
        double? indoorTemperature = 23, double? indoorHumidity = 57, double? target = 50) =>
        new(outdoorTemperature, outdoorHumidity, gust, rain, warning, dark, indoorTemperature, indoorHumidity, target);

    [Fact]
    public void AbsoluteHumidity_IsTheWaterInTheAir()
    {
        WindowAdvice.AbsoluteHumidity(20, 50).Should().BeApproximately(8.6, 0.1);
        WindowAdvice.AbsoluteHumidity(23, 57).Should().BeApproximately(11.7, 0.2);
        WindowAdvice.AbsoluteHumidity(0, 100).Should().BeApproximately(4.8, 0.1);
    }

    [Fact]
    public void MildAndDrierOutside_AiringIsFine()
    {
        var advice = WindowAdvice.Advise(Mild());

        advice.Verdict.Should().Be(WindowVerdict.Ventilate);
        advice.Title.Should().Be("Luchten kan");
        advice.Minutes.Should().Be(WindowAdvice.NormalMinutes);
        advice.Reasons.Should().Contain(r => r.Contains("droger dan binnen"));
    }

    [Fact]
    public void ColderAirOutsideCanBeDrierDespiteAHigherPercentage_AndTheReasonSaysSo()
    {
        // 0° and 70% outside is 3,4 g/m³; 24° and 46% inside is 10,0 g/m³. Warmed up to 24° the air from outside is about 16%
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: 0, outdoorHumidity: 70, indoorTemperature: 24, indoorHumidity: 46));

        advice.Verdict.Should().Be(WindowVerdict.VentilateShort);
        advice.Reasons.Should().Contain(r => r.Contains("droger dan binnen") && r.Contains("opgewarmd tot 24° is hij nog maar 16% vochtig"));
    }

    [Fact]
    public void ColdOutside_IsAShortAiring()
    {
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: 1, outdoorHumidity: 66));

        advice.Verdict.Should().Be(WindowVerdict.VentilateShort);
        advice.Title.Should().Be("Kort luchten");
        advice.Minutes.Should().Be(WindowAdvice.ShortMinutes);
    }

    [Fact]
    public void ColdLimit_IsInclusive()
    {
        WindowAdvice.Advise(Mild(outdoorTemperature: WindowAdvice.ColdLimit, outdoorHumidity: 60)).Verdict.Should().Be(WindowVerdict.VentilateShort);
        WindowAdvice.Advise(Mild(outdoorTemperature: WindowAdvice.ColdLimit + 0.1, outdoorHumidity: 60)).Verdict.Should().Be(WindowVerdict.Ventilate);
    }

    [Theory]
    [InlineData(95)]
    [InlineData(90)]
    public void VeryDampOutside_AiringDoesNotDryTheHouse(double humidity)
    {
        // At 95% outside nothing dries, however the temperatures compare
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: 4, outdoorHumidity: humidity));

        advice.Verdict.Should().Be(WindowVerdict.LittleUse);
        advice.Title.Should().Be("Weinig nut");
        advice.Reasons.Should().Contain(r => r.Contains($"{humidity}% vochtig"));
        advice.Reasons.Should().Contain("Laat de ontvochtiger zijn werk doen");
    }

    [Fact]
    public void JustBelowTheDampLimit_IsStillFine()
    {
        WindowAdvice.Advise(Mild(outdoorTemperature: 10, outdoorHumidity: 89)).Verdict.Should().Be(WindowVerdict.Ventilate);
    }

    [Fact]
    public void WarmAndHumidOutside_IsNotDrierThanInside()
    {
        // 22° and 80% is 15,6 g/m³ against 11,7 inside: more water outside
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: 22, outdoorHumidity: 80));

        advice.Verdict.Should().Be(WindowVerdict.LittleUse);
        advice.Reasons.Should().Contain(r => r.Contains("niet droger dan binnen"));
    }

    [Fact]
    public void OutsideOnlySlightlyDrier_IsBelowTheMinimumGain()
    {
        // Inside 11,7 g/m³; outside must be at least 1,5 below that
        var inside = WindowAdvice.AbsoluteHumidity(23, 57);
        var outsideAt = (double temperature) => 100 * (inside - 1.0) / WindowAdvice.AbsoluteHumidity(temperature, 100);

        WindowAdvice.Advise(Mild(outdoorTemperature: 15, outdoorHumidity: outsideAt(15))).Verdict.Should().Be(WindowVerdict.LittleUse);
    }

    [Fact]
    public void WithoutIndoorValues_OnlyTheDampLimitDecides()
    {
        var advice = WindowAdvice.Advise(Mild(indoorTemperature: null, indoorHumidity: null, outdoorHumidity: 80));

        advice.Verdict.Should().Be(WindowVerdict.Ventilate);
        advice.IndoorAbsoluteHumidity.Should().BeNull();
    }

    [Fact]
    public void Rain_IsBetterClosed()
    {
        var advice = WindowAdvice.Advise(Mild(rain: true));

        advice.Verdict.Should().Be(WindowVerdict.KeepClosed);
        advice.Title.Should().Be("Liever dicht");
        advice.Reasons.Should().Contain("Er wordt regen verwacht");
    }

    [Fact]
    public void AWeatherWarning_IsBetterClosed()
    {
        WindowAdvice.Advise(Mild(warning: true)).Verdict.Should().Be(WindowVerdict.KeepClosed);
    }

    [Fact]
    public void Gusts_FromTheLimit_AreBetterClosed()
    {
        WindowAdvice.Advise(Mild(gust: WindowAdvice.GustLimit)).Verdict.Should().Be(WindowVerdict.KeepClosed);
        WindowAdvice.Advise(Mild(gust: WindowAdvice.GustLimit - 1)).Verdict.Should().Be(WindowVerdict.Ventilate);
    }

    [Fact]
    public void InTheDarkAndWarm_TheBugsComeIn()
    {
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: WindowAdvice.BugTemperature, outdoorHumidity: 60, dark: true));

        advice.Verdict.Should().Be(WindowVerdict.KeepClosed);
        advice.Reasons.Should().Contain(r => r.Contains("beestjes"));
    }

    [Fact]
    public void InTheDarkButCold_TheWindowsAreStillBetterClosed_WithoutTalkingAboutBugs()
    {
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: WindowAdvice.BugTemperature - 1, outdoorHumidity: 60, dark: true));

        advice.Verdict.Should().Be(WindowVerdict.KeepClosed);
        advice.Reasons.Should().Contain(r => r.Contains("Het is donker")).And.NotContain(r => r.Contains("beestjes"));
    }

    [Theory]
    [InlineData(-6, 80)]
    [InlineData(-2, 78)]
    [InlineData(2, 70)]
    public void AtNightInTheWinter_NeverAdviseToAir_EvenWhenTheAirOutsideIsMuchDrier(double temperature, double humidity)
    {
        // The night of 26 to 27 November: -6° and 80% outside is 2,9 g/m³ against 8 to 12 inside, and it was dark all night
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: temperature, outdoorHumidity: humidity, dark: true));

        advice.Verdict.Should().Be(WindowVerdict.KeepClosed);
        advice.Title.Should().Be("Liever dicht");
        advice.Minutes.Should().BeNull();
    }

    [Fact]
    public void InTheDark_TheOtherReasonsStayInTheAdvice()
    {
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: 4, outdoorHumidity: 95, dark: true, rain: true));

        advice.Verdict.Should().Be(WindowVerdict.KeepClosed);
        advice.Reasons.Should().Contain("Er wordt regen verwacht").And.Contain(r => r.Contains("Het is donker")).And.Contain(r => r.Contains("95% vochtig"));
    }

    [Fact]
    public void TheRisks_AreTheReasonsOfTheAdvice()
    {
        var conditions = Mild(outdoorTemperature: 14, gust: 60, rain: true, warning: true, dark: true);

        var risks = WindowAdvice.Risks(conditions);

        risks.Select(r => r.Kind).Should().Equal("waarschuwing", "regen", "wind", "donker");
        WindowAdvice.Advise(conditions).Reasons.Should().Contain(risks.Select(r => r.Reason));
    }

    [Fact]
    public void WithoutRisks_ThereAreNoReasonsToKeepTheWindowsClosed()
    {
        WindowAdvice.Risks(Mild()).Should().BeEmpty();
    }

    [Fact]
    public void ByDayAndWarm_BugsAreNoReason()
    {
        WindowAdvice.Advise(Mild(outdoorTemperature: 15, outdoorHumidity: 50, dark: false)).Verdict.Should().Be(WindowVerdict.Ventilate);
    }

    [Fact]
    public void BetterClosed_BeatsLittleUse_AndKeepsBothReasons()
    {
        var advice = WindowAdvice.Advise(Mild(rain: true, outdoorHumidity: 95));

        advice.Verdict.Should().Be(WindowVerdict.KeepClosed);
        advice.Reasons.Should().Contain("Er wordt regen verwacht").And.Contain(r => r.Contains("95% vochtig"));
    }

    [Fact]
    public void IndoorAlreadyDry_IsMentioned()
    {
        var advice = WindowAdvice.Advise(Mild(indoorHumidity: 48, target: 50));

        advice.Verdict.Should().Be(WindowVerdict.Ventilate);
        advice.Reasons.Should().Contain(r => r.Contains("voor het vocht hoeft het niet"));
    }

    [Fact]
    public void WithoutWeather_TheAdviceIsUnknown()
    {
        var advice = WindowAdvice.Advise(Mild(outdoorTemperature: null, outdoorHumidity: null));

        advice.Verdict.Should().Be(WindowVerdict.Unknown);
        advice.Key.Should().Be("onbekend");
    }

    [Theory]
    [InlineData(WindowVerdict.Ventilate, "luchten")]
    [InlineData(WindowVerdict.VentilateShort, "kort")]
    [InlineData(WindowVerdict.LittleUse, "weinig_nut")]
    [InlineData(WindowVerdict.KeepClosed, "dicht")]
    public void EveryVerdict_HasAKeyForTheDashboard(WindowVerdict verdict, string key)
    {
        new WindowAdviceResult(verdict, "", [], null, null, null).Key.Should().Be(key);
    }
}
