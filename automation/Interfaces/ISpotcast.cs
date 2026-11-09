namespace Automation.Interfaces;

public interface ISpotcast
{
    /// <summary>Starts a playlist on a speaker.</summary>
    /// <param name="mediaPlayer">The speaker.</param>
    /// <param name="spotifyUrl">The playlist (a Spotify link or URI).</param>
    /// <param name="account">The Spotcast account (its config entry id); the default account when null.</param>
    void PlaySpotify(MediaPlayerEntity mediaPlayer, string spotifyUrl, string? account = null);
}