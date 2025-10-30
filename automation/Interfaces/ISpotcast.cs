using Automation.Models.DiscordNotificationModels;

namespace Automation.Interfaces;

public interface ISpotcast
{
    void PlaySpotify(MediaPlayerEntity mediaPlayer, string spotifyUrl);
}