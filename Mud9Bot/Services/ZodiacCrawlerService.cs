using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public record ZodiacScrapeResult(
    string Summary, 
    Dictionary<string, (int Score, string Text)> Categories
);

public interface IZodiacCrawlerService
{
    Task<Dictionary<int, ZodiacScrapeResult>> FetchAllSignsAsync(string dateStr);
}

public class ZodiacCrawlerService(ILogger<ZodiacCrawlerService> logger) : IZodiacCrawlerService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<Dictionary<int, ZodiacScrapeResult>> FetchAllSignsAsync(string dateStr)
    {
        var results = new Dictionary<int, ZodiacScrapeResult>();

        for (int i = 0; i < 12; i++)
        { 
            try
            {
                results[i] = await FetchSignAsync(dateStr, i);
                await Task.Delay(300); // Anti-ban delay
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
        string url = $"http://astro.click108.com.tw/daily_{index}.php?iAcDay={date}&iAstro={index}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // 1. Scrape Lucky Items
        // Click108 結構通常為: <h4><span class="LUCKY">幸運數字：</span>8</h4>
        var luckyNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'TODAY_LUCKY')]//h4");
        
        string ParseLuckyNode(HtmlNodeCollection? nodes, int nodeIndex)
        {
            if (nodes == null || nodes.Count <= nodeIndex) return "-";
            var text = nodes[nodeIndex].InnerText;
            // 用全形或半形冒號切開，取最後一段內容
            var parts = text.Split(new[] { '：', ':' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.LastOrDefault()?.Trim() ?? "-";
        }

        string luckyNumber = ParseLuckyNode(luckyNodes, 0);
        string luckyColor = ParseLuckyNode(luckyNodes, 1);
        string luckyZodiac = ParseLuckyNode(luckyNodes, 4);

        // 2. Scrape Summary
        string todayWord = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'TODAY_WORD')]//p")?.InnerText ?? "";

        // 3 & 4. Scrape Scores and Content (COMBINED FIX)
        // Click108 alternates <p> tags inside TODAY_CONTENT:
        // Index 0 (Even): "整體運勢：★★★★☆" -> Contains the score
        // Index 1 (Odd) : "實際內容..." -> Contains the text
        var contentNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'TODAY_CONTENT')]//p");
        var scores = new List<int>();
        var contents = new List<string>();

        if (contentNodes != null)
        {
            for (int i = 0; i < contentNodes.Count; i++)
            {
                string text = contentNodes[i].InnerText.Trim();
                
                if (i % 2 == 0) 
                {
                    // This is a header row, extract the score by counting stars
                    int score = text.Count(c => c == '★' || c == '⭐');
                    scores.Add(score);
                }
                else 
                {
                    // This is a content row
                    contents.Add(text);
                }
            }
        }

        string zodiacName = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'TODAY_CONTENT')]//h3")?.InnerText ?? "";

        string summary = $"<b>{zodiacName} ({date})</b>\n今日短評: {todayWord}\n幸運數字: {luckyNumber}\n幸運顏色: {luckyColor}\n幸運星座: {luckyZodiac}";

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