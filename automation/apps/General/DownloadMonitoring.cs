using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;
using Automation.Models.Yts;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that monitors YTS feeds and sends notifications for new downloads.
/// </summary>
[NetDaemonApp(Id = nameof(DownloadMonitoring))]
public partial class DownloadMonitoring : BaseApp
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadMonitoring"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="dataRepository">The data repository for storing and retrieving data.</param>
    public DownloadMonitoring(
        IHaContext haContext,
        ILogger<DownloadMonitoring> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository dataRepository)
        : base(haContext, logger, notify, scheduler)
    {
       YtsMonitoring(notify, dataRepository, "yts2160p", Entities.Sensor.YtsFeed2160p);

       Entities.Sensor.YtsFeed1080.StateChanges()
           .Subscribe(_ =>
           {
               YtsMonitoring(notify, dataRepository, "yts1080", Entities.Sensor.YtsFeed1080);
           });
       Entities.Sensor.YtsFeed2160p.StateChanges()
           .Subscribe(_ =>
           {
               YtsMonitoring(notify, dataRepository, "yts2160p", Entities.Sensor.YtsFeed2160p);
           });
    }

    /// <summary>
    /// Monitors the YTS feed and sends notifications for new downloads.
    /// </summary>
    /// <param name="notify">The notification service.</param>
    /// <param name="dataRepository">The data repository for storing and retrieving data.</param>
    /// <param name="saveId">The identifier for saving data.</param>
    /// <param name="feed">The sensor entity representing the YTS feed.</param>
    private static void YtsMonitoring(INotify notify, IDataRepository dataRepository, string saveId, SensorEntity feed )
    {
            if (feed.Attributes?.Entries != null)
            {
                var discordChannel = ConfigManager.GetValueFromConfigNested("Discord", "Yts") ?? "";

                var items = feed.Attributes?.Entries!.Cast<JsonElement>()
                    .Select(o => o.Deserialize<Yts>()).ToList();

                var thisYear = DateTimeOffset.Now.Year;
                var lastYear = DateTimeOffset.Now.AddYears(-1).Year;

                if (items != null)
                {
                    var oldList = dataRepository.Get<List<Yts>>(saveId);

                    foreach (var discordModel in from ytsItem in items
                             where ytsItem != null
                             where oldList == null || oldList.TrueForAll(yts => yts.Id != ytsItem.Id)
                             where ytsItem.Title.Contains(thisYear.ToString()) ||
                                   ytsItem.Title.Contains(lastYear.ToString())
                             let downloadLink = ytsItem.Links.First(link => link.Type == "application/x-bittorrent")
                                 .Href
                             let image = GetTextFromHtmlRegex(ytsItem.Summary, ImgRegex())
                             let imbdRating = GetTextFromHtmlRegex(ytsItem.Summary, ImdbRatingRegex())
                             let genre = GetTextFromHtmlRegex(ytsItem.Summary, GenreRegex())
                             let size = GetTextFromHtmlRegex(ytsItem.Summary, SizeRegex())
                             let runtime = GetTextFromHtmlRegex(ytsItem.Summary, RuntimeRegex())
                             select new DiscordNotificationModel
                             {
                                 Embed = new Embed
                                 {
                                     Title = ytsItem.Title,
                                     Url = ytsItem.Link,
                                     Thumbnail = new Location(image),
                                     Fields =
                                     [
                                         new Field { Name = "Rating", Value = imbdRating },
                                         new Field { Name = "Genre", Value = genre },
                                         new Field { Name = "Size", Value = size },
                                         new Field { Name = "Runtime", Value = runtime },
                                         new Field { Name = "Direct Download", Value = downloadLink }
                                     ]
                                 },
                                 Urls = [downloadLink]
                             })
                    {
                        notify.NotifyDiscord("", [discordChannel], discordModel);
                    }

                    dataRepository.Save(saveId, items);
                }
            }
    }

    /// <summary>
    /// Extracts text from HTML using a regular expression.
    /// </summary>
    /// <param name="htmlSource">The HTML source string.</param>
    /// <param name="regex">The regular expression to use for extraction.</param>
    /// <returns>The extracted text.</returns>
    private static string GetTextFromHtmlRegex(string htmlSource, Regex regex)
    {
        var matchesImgSrc = regex.Matches(htmlSource);
        var match = matchesImgSrc[0];
        return match.Groups.Count == 2 ? match.Groups[1].Value : match.Groups[2].Value;
    }

    /// <summary>
    /// Gets the regular expression for extracting image URLs from HTML.
    /// </summary>
    /// <returns>The regular expression for image URLs.</returns>
    [GeneratedRegex("<img[^>]*?src\\s*=\\s*[\"']?([^'\" >]+?)[ '\"][^>]*?>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-NL")]
    private static partial Regex ImgRegex();

    /// <summary>
    /// Gets the regular expression for extracting IMDB ratings from HTML.
    /// </summary>
    /// <returns>The regular expression for IMDB ratings.</returns>
    [GeneratedRegex("(IMDB Rating:)(.+?)(?=<)", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-NL")]
    private static partial Regex ImdbRatingRegex();

    /// <summary>
    /// Gets the regular expression for extracting genres from HTML.
    /// </summary>
    /// <returns>The regular expression for genres.</returns>
    [GeneratedRegex("(Genre:)(.+?)(?=<)", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-NL")]
    private static partial Regex GenreRegex();

    /// <summary>
    /// Gets the regular expression for extracting sizes from HTML.
    /// </summary>
    /// <returns>The regular expression for sizes.</returns>
    [GeneratedRegex("(Size:)(.+?)(?=<)", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-NL")]
    private static partial Regex SizeRegex();

    /// <summary>
    /// Gets the regular expression for extracting runtimes from HTML.
    /// </summary>
    /// <returns>The regular expression for runtimes.</returns>
    [GeneratedRegex("(Runtime:)(.+?)(?=<)", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-NL")]
    private static partial Regex RuntimeRegex();
}