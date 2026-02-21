using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;
using Mud9Bot.Models;

namespace Mud9Bot.Services;

public class MarkSixService(ILogger<MarkSixService> logger, HttpClient httpClient) : IMarkSixService
{
    private MarkSixResult? _cache;
    private const string TargetUrl = "https://mark6.app/now";

    public MarkSixResult? GetLatestResult() => _cache;

    public async Task UpdateCacheAsync()
    {
        try
        {
            var html = await httpClient.GetStringAsync(TargetUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new MarkSixResult();

            // 1. 解析期數與日期 (例如: 第 020 期 21/02/2026)
            var periodNode = doc.DocumentNode.SelectSingleNode("//p[contains(@class, 'w3-center')][contains(text(), '期')]");
            result.Period = periodNode?.InnerText.Trim() ?? "未知期數";

            // 2. 解析號碼 (前 6 個是正碼，第 7 個是特別號碼)
            var ballNodes = doc.DocumentNode.SelectNodes("//span[contains(@class, 'ball')]");
            if (ballNodes != null && ballNodes.Count >= 7)
            {
                for (int i = 0; i < 6; i++)
                {
                    result.Numbers.Add(ballNodes[i].InnerText.Trim());
                }
                result.SpecialBall = ballNodes[6].InnerText.Trim();
            }

            // 3. 解析獎金分配
            var prizeNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'w3-sand')]//p[contains(text(), '奬')]");
            if (prizeNodes != null)
            {
                foreach (var node in prizeNodes)
                {
                    result.Prizes.Add(node.InnerText.Trim().Replace("&nbsp;", " "));
                }
            }

            // 4. 解析下期預告 (黃色區塊)
            var nextDrawNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'w3-yellow')]//p");
            if (nextDrawNodes != null)
            {
                foreach (var node in nextDrawNodes)
                {
                    string text = node.InnerText.Trim();
                    if (text.Contains("截止售票")) result.NextDrawTime = text.Replace("下期截止售票：", "").Trim();
                    if (text.Contains("估計彩金")) result.NextJackpot = text.Replace("下期估計彩金：", "").Trim();
                }
            }

            result.LastUpdated = DateTime.UtcNow;
            _cache = result;
            
            logger.LogInformation("Mark Six cache updated. Period: {Period}", result.Period);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scrape Mark Six results.");
        }
    }
}