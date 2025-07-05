using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;
using Automation.Models.Twitter;
using RestSharp;

namespace Automation.apps.General;
[NetDaemonApp(Id = nameof(CocMonitoring))]
public class CocMonitoring : BaseApp
{
    public CocMonitoring(IHaContext haContext,  ILogger<CocMonitoring> logger, INotify notify, IScheduler scheduler, IDataRepository dataRepository) : base(haContext, logger, notify, scheduler)
    {
        var discordChannel = ConfigManager.GetValueFromConfigNested("Discord", "COC") ?? "";
        var bearerToken = ConfigManager.GetValueFromConfigNested("Twitter", "BearerToken") ?? "";

        _ = GetTweets(bearerToken, discordChannel, dataRepository); 
        
        Scheduler.RunDaily(TimeSpan.Parse("07:00:00", new CultureInfo("nl-Nl")),
            async () => { await GetTweets(bearerToken, discordChannel, dataRepository); });        
        Scheduler.RunDaily(TimeSpan.Parse("19:00:00", new CultureInfo("nl-Nl")),
            async () => { await GetTweets(bearerToken, discordChannel, dataRepository); });
    }

    private async Task GetTweets(string bearerToken, string discordChannel, IDataRepository dataRepository)
    {
        const string twitterUserId = "1671940215013867529";

        var idList = await dataRepository.GetAsync<List<string>>("COC_TWEET_ID_LIST") ?? [];
        
        var options = new RestClientOptions("https://api.twitter.com");
        var client = new RestClient(options);
        var request = new RestRequest($"/2/users/{twitterUserId}/tweets");
        request.AddHeader("Authorization", $"Bearer {bearerToken}");
        var response = client.Execute(request);

        if (response.IsSuccessful)
        {
            if (!string.IsNullOrEmpty(response.Content))
            {
                var twitterModel = JsonSerializer.Deserialize<TweetResponse>(response.Content);
                if (twitterModel?.Data != null)
                    foreach (var tweet in twitterModel.Data)
                    {
                        if (tweet.Id != null && !idList.Contains(tweet.Id))
                        {
                            SendToDiscord(discordChannel, tweet);
                            idList.Add(tweet.Id);
                        }
                    }
            }
            await dataRepository.SaveAsync("COC_TWEET_ID_LIST", idList);
        }
    }


    private void SendToDiscord(string discordChannel, Tweet tweet)
    {
        if (tweet.Text != null)
        {
            var url = UrlExtractor.ExtractUrls(tweet.Text);
            
            var discordModel = new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Title = "New Tweet",
                    Url = url.FirstOrDefault(),
                    Thumbnail = new Location("https://www.pngitem.com/pimgs/m/32-325072_logo-coc-png-clash-of-clans-logo-transparent.png"),
                    Description = tweet.Text
                },
                Urls = url.ToArray()
            };

            Notify.NotifyDiscord("", [discordChannel], discordModel);
        }
    }
}