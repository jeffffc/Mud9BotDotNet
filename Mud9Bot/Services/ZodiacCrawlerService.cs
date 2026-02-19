using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public record ZodiacScrapeResult(
    string Summary, 
    Dictionary<string, (int Score, string Text)> Categories
);

public interface IZodiacCrawlerService
{
    /// <summary>
    /// Fetches all 12 signs for a specific date (yyyy-MM-dd)
    /// </summary>
    Task<Dictionary<int, ZodiacScrapeResult>> FetchAllSignsAsync(string dateStr);
}

public class ZodiacCrawlerService(ILogger<ZodiacCrawlerService> logger) : IZodiacCrawlerService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<Dictionary<int, ZodiacScrapeResult>> FetchAllSignsAsync(string dateStr)
    {
        var results = new Dictionary<int, ZodiacScrapeResult>();

        // Porting the 0-11 loop from your Python logic
        for (int i = 0; i < 12; i++)
        {
            try
            {
                var data = await FetchSignAsync(dateStr, i);
                results[i] = data;
                
                // Be a good citizen: small delay between requests to avoid IP bans
                await Task.Delay(300); 
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching zodiac {Index} for {Date}", i, dateStr);
            }
        }

        return results;
    }

    private async Task<ZodiacScrapeResult> FetchSignAsync(string date, int index)
    {
        // URL format matches your Python find(day, zodiacnum) function
        string url = $"http://astro.click108.com.tw/daily_{index}.php?iAcDay={date}&iAstro={index}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // 1. Lucky Info (TODAY_LUCKY)
        var luckyNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'TODAY_LUCKY')]//span[contains(@class, 'LUCKY')]");
        string luckyNumber = luckyNodes?[0].SelectSingleNode(".//h4")?.InnerText ?? "-";
        string luckyColor = luckyNodes?[1].SelectSingleNode(".//h4")?.InnerText ?? "-";
        string luckyZodiac = luckyNodes?[4].SelectSingleNode(".//h4")?.InnerText ?? "-";

        // 2. Summary Word (TODAY_WORD)
        string todayWord = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'TODAY_WORD')]//p")?.InnerText ?? "";

        // 3. Scores (STAR_LIGHT) - Regex to find the star icon number (iconX.png)
        var starNodes = doc.DocumentNode.SelectNodes("//span[contains(@class, 'STAR_LIGHT')]");
        var scores = new List<int>();
        if (starNodes != null)
        {
            foreach (var node in starNodes)
            {
                var match = Regex.Match(node.OuterHtml, @"icon\d(\d).png");
                if (match.Success) scores.Add(int.Parse(match.Groups[1].Value));
            }
        }

        // 4. Detailed Content (p tags inside TODAY_CONTENT)
        // Replicating your Python logic: taking every second 'p' tag (indices 1, 3, 5, 7)
        var contentNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'TODAY_CONTENT')]//p");
        var contents = new List<string>();
        if (contentNodes != null)
        {
            for (int i = 0; i < contentNodes.Count; i++)
            {
                if (i % 2 == 1) // This picks up the actual description text
                {
                    contents.Add(contentNodes[i].InnerText.Trim());
                }
            }
        }

        string zodiacName = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'TODAY_CONTENT')]//h3")?.InnerText ?? "";

        // Build the Summary text (Matches your Python 'msg')
        string summary = $"{zodiacName} ({date})\n今日短評: {todayWord}\n幸運數字: {luckyNumber}\n幸運顏色: {luckyColor}\n幸運星座: {luckyZodiac}";

        // Map categories to your legacy keys
        var categories = new Dictionary<string, (int Score, string Text)>
        {
            { "overall", (scores.ElementAtOrDefault(0), contents.ElementAtOrDefault(0) ?? "無資料") },
            { "love",    (scores.ElementAtOrDefault(1), contents.ElementAtOrDefault(1) ?? "無資料") },
            { "career",  (scores.ElementAtOrDefault(2), contents.ElementAtOrDefault(2) ?? "無資料") },
            { "money",   (scores.ElementAtOrDefault(3), contents.ElementAtOrDefault(3) ?? "無資料") }
        };

        return new ZodiacScrapeResult(summary, categories);
    }
}