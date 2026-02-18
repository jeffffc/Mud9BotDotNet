using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class TrafficService(IHttpService httpService, ILogger<TrafficService> logger) : ITrafficService
{
    private const string TrafficUrl = "https://programme.rthk.hk/channel/radio/trafficnews/index.php";

    public async Task<string> GetTrafficNewsAsync(CancellationToken ct = default)
    {
        try
        {
            var html = await httpService.GetStringAsync(TrafficUrl, ct);
            if (string.IsNullOrEmpty(html)) return "❌ Failed to fetch traffic news (Empty response).";

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // RTHK structure: Items are usually in <li> tags or inside a specific div structure.
            // Based on previous analysis, we look for list items.
            var listItems = doc.DocumentNode.SelectNodes("//div[@class='inner']//li | //div[@class='traffic_news']//li | //li"); 
            // The selector might need adjustment if RTHK changes layout, but usually it's in a list.
            // Let's try a generic <li> scan and filter by timestamp pattern as it's most robust.
            
            if (listItems == null) return "⚠️ No traffic news found (Layout changed?)";

            var sb = new StringBuilder();
            int count = 0;
            var timeRegex = new Regex(@"(\d{4}-\d{2}-\d{2} HKT \d{2}:\d{2})");

            foreach (var node in listItems)
            {
                var text = node.InnerText.Trim();
                var match = timeRegex.Match(text);

                if (match.Success)
                {
                    // Clean up HTML entities (e.g. &nbsp;)
                    text = System.Net.WebUtility.HtmlDecode(text);
                    
                    var fullTime = match.Value;
                    var shortTime = fullTime.Split(' ').Last(); // Get "HH:MM"
                    
                    // The text usually ends with the timestamp. We remove it for cleaner display.
                    var content = text.Replace(fullTime, "").Trim().TrimEnd(',').Trim();

                    sb.AppendLine($"`{shortTime}`: {content}\n");
                    count++;

                    if (count >= 5) break; // Limit to 5 items
                }
            }

            return sb.Length > 0 ? sb.ToString() : "✅ No traffic news at the moment.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching traffic news");
            return $"❌ Error fetching traffic news: {ex.Message}";
        }
    }
}