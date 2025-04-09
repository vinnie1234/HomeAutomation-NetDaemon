using System.Text.RegularExpressions;

namespace Automation.Extensions;

public class UrlExtractor
{
    public static List<string> ExtractUrls(string input)
    {
        var urls = new List<string>();
        // Regex voor URL's
        var pattern = @"https?://[^\s]+";
        var matches = Regex.Matches(input, pattern);

        foreach (Match match in matches)
        {
            urls.Add(match.Value);
        }

        return urls;
    }

}