using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;
using Mud9Bot.Models;

namespace Mud9Bot.Services;

public class GasService(HttpClient httpClient, ILogger<GasService> logger) : IGasService
{
    private List<GasPriceData> _cache = new();
    public DateTime LastUpdated { get; private set; } = DateTime.MinValue;

    private const string ApiUrl = "https://www.consumer.org.hk/pricewatch/oilwatch/opendata/oilprice.json";

    public List<GasPriceData> GetCachedPrices() => _cache;

    public async Task UpdatePricesAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("正在從消委會抓取最新油價數據...");
            
            // 加入 User-Agent 以防被阻擋
            var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            request.Headers.Add("User-Agent", "Mud9Bot-Revival/1.0");

            var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<List<GasPriceData>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, ct);

            if (data != null && data.Any())
            {
                _cache = data;
                LastUpdated = DateTime.UtcNow;
                logger.LogInformation("油價數據更新成功，共載入 {Count} 類油品。", _cache.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "抓取消委會油價失敗。");
        }
    }
}