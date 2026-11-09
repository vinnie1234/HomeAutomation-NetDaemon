namespace Automation.apps;

public class Spotcast(IHaContext rawHa) : ISpotcast
{
    // What this plays is written to the activity log as the app Spotcast
    private readonly IHaContext ha = new Core.ActivityHaContext(rawHa, nameof(Spotcast));
    private readonly Entities _entities = new(rawHa);

    public void PlaySpotify(MediaPlayerEntity mediaPlayer, string spotifyUrl, string? account = null)
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

            if (!string.IsNullOrEmpty(account)) serviceData["account"] = account;

            ha.CallService("spotcast", "play_media", null, serviceData);
        }
    }
}
