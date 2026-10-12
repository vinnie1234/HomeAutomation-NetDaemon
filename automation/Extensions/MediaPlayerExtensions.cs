namespace Automation.Extensions;

public static class MediaPlayerExtensions
{
    /// <summary>
    /// Stops playback, but only when the media player is actually playing (or paused). Google Cast speakers
    /// fail with "Failed to execute stop" when <c>media_stop</c> is sent while nothing is playing.
    /// </summary>
    /// <param name="mediaPlayer">The media player to stop.</param>
    /// <returns><c>true</c> when a stop command was sent; otherwise <c>false</c>.</returns>
    public static bool StopIfPlaying(this MediaPlayerEntity mediaPlayer)
    {
        if (mediaPlayer.State is not ("playing" or "paused")) return false;

        mediaPlayer.MediaStop();
        return true;
    }

    /// <summary>
    /// Pauses playback, but only when the media player is actually playing. Google Cast speakers fail
    /// when <c>media_pause</c> is sent while nothing is playing.
    /// </summary>
    /// <param name="mediaPlayer">The media player to pause.</param>
    /// <returns><c>true</c> when a pause command was sent; otherwise <c>false</c>.</returns>
    public static bool PauseIfPlaying(this MediaPlayerEntity mediaPlayer)
    {
        if (mediaPlayer.State != "playing") return false;

        mediaPlayer.MediaPause();
        return true;
    }
}
