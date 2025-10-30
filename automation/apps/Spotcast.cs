namespace Automation.apps;

public class Spotcast(IHaContext ha) : ISpotcast
{
    public void PlaySpotify(MediaPlayerEntity mediaPlayer, string spotifyUrl)
    {
        var deviceId = mediaPlayer.Registration?.Device?.Id;
 
        if (deviceId != null)
        {
            var serviceData = new Dictionary<string, object>
            {
                { "media_player", new Dictionary<string, object> {
                    { "device_id", deviceId }
                }},
                { "spotify_uri", spotifyUrl },
                { "data", new Dictionary<string, object> {
                    { "shuffle", true }
                }}
            };

            ha.CallService("spotcast", "play_media", null, serviceData);
        }
    }
}