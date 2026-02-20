using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public record TrafficCamera(string Id, string Description);
public record TrafficDistrict(string Name, List<TrafficCamera> Cameras);
public record TrafficRegion(string Name, List<TrafficDistrict> Districts);

public class TrafficService(HttpClient httpClient, ILogger<TrafficService> logger) : ITrafficService
{
    private const string TrafficNewsUrl = "https://programme.rthk.hk/channel/radio/trafficnews/index.php";
    private const string SnapshotApiUrl = "https://static.data.gov.hk/td/traffic-snapshot-images/code/Traffic_Camera_Locations_Tc.xml";

    private List<TrafficRegion> _regionCache = new();

    // --- 1. RTHK 交通消息實作 ---
    public async Task<string> GetTrafficNewsAsync(CancellationToken ct = default)
    {
        try
        {
            var html = await httpClient.GetStringAsync(TrafficNewsUrl, ct);
            if (string.IsNullOrEmpty(html)) return "❌ 無法獲取交通消息 (Empty response)。";

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var listItems = doc.DocumentNode.SelectNodes("//div[@class='inner']//li | //div[@class='traffic_news']//li | //li"); 
            
            if (listItems == null) return "⚠️ 暫時無交通消息 (可能網頁結構已改變)。";

            var sb = new StringBuilder();
            int count = 0;
            var timeRegex = new Regex(@"(\d{4}-\d{2}-\d{2} HKT \d{2}:\d{2})");

            foreach (var node in listItems)
            {
                var text = node.InnerText.Trim();
                var match = timeRegex.Match(text);

                if (match.Success)
                {
                    text = WebUtility.HtmlDecode(text);
                    var fullTime = match.Value;
                    var shortTime = fullTime.Split(' ').Last();
                    var content = text.Replace(fullTime, "").Trim().TrimEnd(',').Trim();

                    sb.AppendLine($"`{shortTime}`: {content}\n");
                    count++;

                    if (count >= 5) break;
                }
            }

            return sb.Length > 0 ? sb.ToString() : "✅ 目前交通暢順，暫無消息。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching traffic news from RTHK");
            return $"❌ 獲取交通消息時出錯: {ex.Message}";
        }
    }

    // --- 2. 交通快拍實作 ---
    public List<TrafficRegion> GetRegions() => _regionCache;

    public async Task InitializeAsync()
    {
        try
        {
            logger.LogInformation("Fetching Traffic Camera Locations from HK Gov API...");
            
            var bytes = await httpClient.GetByteArrayAsync(SnapshotApiUrl);
            var content = Encoding.UTF8.GetString(bytes);
            
            var xdoc = XDocument.Parse(content);

            // 根據 XML 範例，所有的攝影機都在 <image> 標籤中
            var imageNodes = xdoc.Descendants().Where(e => e.Name.LocalName.Equals("image", StringComparison.OrdinalIgnoreCase));

            // 將平鋪的 XML 轉換為匿名對象清單以便處理
            var flatList = imageNodes.Select(img => new
            {
                Id = img.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("key", StringComparison.OrdinalIgnoreCase))?.Value,
                Region = img.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("region", StringComparison.OrdinalIgnoreCase))?.Value ?? "其他",
                District = img.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("district", StringComparison.OrdinalIgnoreCase))?.Value ?? "其他",
                Desc = img.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("description", StringComparison.OrdinalIgnoreCase))?.Value
            })
            .Where(x => !string.IsNullOrEmpty(x.Id))
            .ToList();

            if (!flatList.Any())
            {
                logger.LogWarning("Traffic Snapshot XML parsed but 0 images found. Content length: {Length}", content.Length);
                _regionCache = new List<TrafficRegion>();
                return;
            }

            // 使用 LINQ 進行多層分組：Region -> District -> Camera
            var groupedRegions = flatList
                .GroupBy(f => f.Region)
                .Select(regGroup => new TrafficRegion(
                    regGroup.Key,
                    regGroup.GroupBy(f => f.District)
                        .Select(distGroup => new TrafficDistrict(
                            distGroup.Key,
                            distGroup.Select(f => new TrafficCamera(f.Id!, f.Desc ?? f.Id!)).ToList()
                        ))
                        .OrderBy(d => d.Name)
                        .ToList()
                ))
                .OrderBy(r => r.Name)
                .ToList();

            _regionCache = groupedRegions;
            logger.LogInformation("Traffic Snapshot data initialized. {Count} regions with total {ImgCount} cameras loaded.", 
                _regionCache.Count, flatList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Traffic data from Gov XML.");
            throw;
        }
    }
}