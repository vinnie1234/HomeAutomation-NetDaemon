namespace Automation.apps;

public class Spotcast(IHaContext ha) : ISpotcast
{
    private readonly Entities _entities = new(ha);

    public void PlaySpotify(MediaPlayerEntity mediaPlayer, string spotifyUrl)
    {
        // Don't play music while Vincent and Carleen are both away (e.g. only a house sitter is home)
        if (_entities.InputBoolean.Away.IsOn()) return;

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
