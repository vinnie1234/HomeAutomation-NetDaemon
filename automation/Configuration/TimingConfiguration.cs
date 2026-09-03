namespace Automation.Configuration;

/// <summary>
/// Configuration for timing delays used throughout the application.
/// </summary>
public class TimingConfiguration
{
    public TimeSpan WelcomeHomeDelay { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan NewYearMusicDelay { get; set; } = TimeSpan.FromSeconds(49);
    public TimeSpan ShortDelay { get; set; } = TimeSpan.FromSeconds(0.5);
}
