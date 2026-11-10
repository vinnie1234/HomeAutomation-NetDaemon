namespace Automation.Extensions;

public static class MediaPlayerExtensions
{
    /// <param name="mediaPlayer">The media player to stop.</param>
    extension(MediaPlayerEntity mediaPlayer)
    {
        /// <summary>
        /// Stops playback, but only when the media player is actually playing (or paused). Google Cast speakers
        /// fail with "Failed to execute stop" when <c>media_stop</c> is sent while nothing is playing.
        /// </summary>
        /// <returns><c>true</c> when a stop command was sent; otherwise <c>false</c>.</returns>
        public bool StopIfPlaying()
        {
            if (mediaPlayer.State is not ("playing" or "paused")) return false;

            mediaPlayer.MediaStop();
            return true;
        }

        /// <summary>
        /// Pauses playback, but only when the media player is actually playing. Google Cast speakers fail
        /// when <c>media_pause</c> is sent while nothing is playing.
        /// </summary>
        /// <returns><c>true</c> when a pause command was sent; otherwise <c>false</c>.</returns>
        public bool PauseIfPlaying()
        {
            if (mediaPlayer.State != "playing") return false;

            mediaPlayer.MediaPause();
            return true;
        }
    }
}
