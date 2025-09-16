using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Helpers;
using Automation.Models.COC;
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
        
        Scheduler.RunDaily(TimeSpan.Parse("07:00:00", new CultureInfo("nl-Nl")),
            () => { GetTweets(bearerToken, discordChannel, dataRepository); });        
        Scheduler.RunDaily(TimeSpan.Parse("19:00:00", new CultureInfo("nl-Nl")),
            () => { GetTweets(bearerToken, discordChannel, dataRepository); });
    }

    private void GetTweets(string bearerToken, string discordChannel, IDataRepository dataRepository)
    {
        // Check if this function has already run today at allowed times (07:00 or 19:00)
        var lastRunTime = dataRepository.Get<string>("COC_LAST_RUN_TIME");
        var now = DateTime.Now;
        
        if (!string.IsNullOrEmpty(lastRunTime) && DateTime.TryParse(lastRunTime, out var lastRun) && lastRun.Date == now.Date)
        {
            var currentHour = now.Hour;
            var lastRunHour = lastRun.Hour;

            switch (lastRunHour)
            {
                // If we already ran at 07:00 and current time is before 19:00, skip
                case 7 when currentHour < 19:
                    Logger.LogDebug("GetTweets already executed today at 07:00, skipping until 19:00");
                    return;
                // If we already ran at 19:00 today, skip
                case 19:
                    Logger.LogDebug("GetTweets already executed today at 19:00, skipping until tomorrow");
                    return;
            }
        }
        
        // Only allow execution at 07:00 (6-8) or 19:00 (18-20) hour ranges
        var currentTime = now.TimeOfDay;
        if (!(currentTime >= TimeSpan.FromHours(6) && currentTime < TimeSpan.FromHours(8)) &&
            !(currentTime >= TimeSpan.FromHours(18) && currentTime < TimeSpan.FromHours(20)))
        {
            Logger.LogWarning("GetTweets can only run between 06:00-08:00 or 18:00-20:00, current time: {Time}", now.ToString("HH:mm:ss"));
            return;
        }

        const string twitterUserId = "1671940215013867529";

        var idListModel = dataRepository.Get<List<COCModel>>("COC_TWEET_ID_LIST") ?? [];
        
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
                        if (tweet.Id != null && idListModel.All(x => x.Id != tweet.Id))
                        {
                            SendToDiscord(discordChannel, tweet);
                            idListModel.Add(new COCModel
                            {
                                Id = tweet.Id,
                                InertDate = DateTime.Now
                            });
                        }
                    }
            }
            dataRepository.Save("COC_TWEET_ID_LIST", idListModel);
        }
        
        // Save the current execution time
        dataRepository.Save("COC_LAST_RUN_TIME", now.ToString("yyyy-MM-dd HH:mm:ss"));
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