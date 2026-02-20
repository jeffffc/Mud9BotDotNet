using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Telegram.Bot.Extensions;

namespace Mud9Bot.Services;

public class StockService : IStockService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockService> _logger;
    
    // 改為 HTTPS 確保連線穩定，避免跳轉導致的 Header 丟失
    private const string StockBaseUrl = "https://www.aastocks.com/tc/mobile/Quote.aspx?symbol={0}";
    private const string IndexApiUrl = "https://www.aastocks.com/tc/resources/datafeed/getstockindex.ashx?type=1";

    public StockService(HttpClient httpClient, ILogger<StockService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // 模擬手機瀏覽器
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1");
        }
        
        // 加入 Referer 是獲取 JSON API 的關鍵
        if (!_httpClient.DefaultRequestHeaders.Contains("Referer"))
        {
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.aastocks.com/tc/mobile/Quote.aspx");
        }
    }

    public async Task<string> GetHsiAsync(CancellationToken ct = default)
    {
        try
        {
            // 優先嘗試 JSON API (手機版最精準的來源)
            var response = await _httpClient.GetAsync(IndexApiUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                
                var hsi = doc.RootElement.EnumerateArray()
                    .FirstOrDefault(e => e.GetProperty("symbol").GetString() == "HSI");

                if (hsi.ValueKind != JsonValueKind.Undefined)
                {
                    string last = hsi.GetProperty("last").GetString() ?? "--";
                    string change = hsi.GetProperty("change").GetString() ?? "--";
                    string sign = hsi.GetProperty("changesign").GetString() ?? "";

                    return $"恆生指數: <code>{last.EscapeHtml()}</code> (<code>{sign}{change.EscapeHtml()}</code>)";
                }
            }
            
            _logger.LogWarning("HSI JSON API returned no data or failed. Falling back to Symbol 5 scraping.");
            
            // 如果 JSON API 失敗，回退到抓取 0005.HK 頁面 (根據您的建議)
            return await GetStockAsync("5", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching HSI");
            return "❌ 獲取恆指失敗，請稍後再試。";
        }
    }

    public async Task<string> GetStockAsync(string code, CancellationToken ct = default)
    {
        try
        {
            // 確保代號格式正確 (不強制補零，因為 AAStocks URL 接受原始輸入)
            string url = string.Format(StockBaseUrl, code);
            var html = await _httpClient.GetStringAsync(url, ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. 股票名稱 (根據您提供的 HTML: td.quote_table_header_text)
            var nameNode = doc.DocumentNode.SelectSingleNode("//td[contains(@class, 'quote_table_header_text')]");
            if (nameNode == null)
            {
                return $"對不起，找不到股票代號 <code>{code.EscapeHtml()}</code>。";
            }

            string name = WebUtility.HtmlDecode(nameNode.InnerText).Replace("&nbsp;", " ").Trim();
            
            // 2. 最後更新 (HTML 中為 font.font12_white)
            var lastUpdateNode = doc.DocumentNode.SelectSingleNode("//font[contains(@class, 'font12_white')]")
                                ?? doc.DocumentNode.SelectSingleNode("//td[contains(@class, 'subheader')]//font");
            string lastUpdate = lastUpdateNode?.InnerText.Trim() ?? "未知";

            // 3. 升跌箭頭 (從 div.text_last 內的 img src 判斷)
            string arrow = "";
            var imgNode = doc.DocumentNode.SelectSingleNode("//div[@class='text_last']//img");
            if (imgNode != null)
            {
                string src = imgNode.GetAttributeValue("src", "").ToLower();
                if (src.Contains("up")) arrow = "↑ ";
                else if (src.Contains("down")) arrow = "↓ ";
            }

            // 4. 提取加密數值 (現價、變動、百分比)
            var priceNode = doc.DocumentNode.SelectSingleNode("//div[@class='text_last']");
            string price = ExtractValue(priceNode);

            var changeRow = doc.DocumentNode.SelectSingleNode("//div[@class='text_last']/following-sibling::div[1]");
            var changeSpans = changeRow?.SelectNodes(".//span");
            
            string change = ExtractValue(changeSpans?.ElementAtOrDefault(0));
            string pct = ExtractValue(changeSpans?.ElementAtOrDefault(1));

            // 5. 成交量與金額 (對應 span.float_right)
            string volume = doc.DocumentNode.SelectSingleNode("//td[contains(., '成交量')]//span[@class='float_right']")?.InnerText.Trim() ?? "--";
            string turnover = doc.DocumentNode.SelectSingleNode("//td[contains(., '成交金額')]//span[@class='float_right']")?.InnerText.Trim() ?? "--";

            var sb = new StringBuilder();
            sb.AppendLine($"<code>{name.EscapeHtml()}</code> (最後更新: <code>{lastUpdate.EscapeHtml()}</code>)");
            sb.AppendLine($"<b>{arrow}{price}</b>  (<code>{change.EscapeHtml()}</code> | <code>{pct.EscapeHtml()}</code>)");
            sb.AppendLine();
            sb.AppendLine($"成交量: <code>{volume.EscapeHtml()}</code>");
            sb.AppendLine($"成交金額: <code>{turnover.EscapeHtml()}</code>");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping AAStocks for code: {Code}", code);
            return "❌ 暫時拎唔到股價資料，可能網頁結構已改變。";
        }
    }

    private string ExtractValue(HtmlNode? node)
    {
        if (node == null) return "--";

        // 搜尋節點下所有 script 標籤，匹配 7 位大寫字母的加密函數
        var scripts = node.Descendants("script");
        foreach (var script in scripts)
        {
            var match = Regex.Match(script.InnerText, @"[A-Z]{7}\('([^']+)'\)");
            if (match.Success) return match.Groups[1].Value;
        }

        string text = WebUtility.HtmlDecode(node.InnerText).Trim();
        if (text.Contains("document.write")) return "--";
        
        return string.IsNullOrEmpty(text) ? "--" : text;
    }
}