using System.Text.RegularExpressions;

namespace Automation.Extensions;

public static partial class UrlExtractor
{
    public static List<string> ExtractUrls(string input)
    {
        var urls = new List<string>();
        var matches = UrlRegex().Matches(input);

        foreach (Match match in matches)
        {
            urls.Add(match.Value);
        }

        return urls;
    }

    [GeneratedRegex(@"https?://[^\s]+")]
    private static partial Regex UrlRegex();
}