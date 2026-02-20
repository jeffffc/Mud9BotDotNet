using Microsoft.Extensions.Logging;
using Mud9Bot.Models;
using Mud9Bot.Interfaces;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Mud9Bot.Services;

public class WeatherCrawlerService(ILogger<WeatherCrawlerService> logger) : IWeatherCrawlerService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const string ApiUrl = "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=rhrread&lang=tc";

    public async Task<WeatherData?> FetchWeatherAsync()
    {
        try
        {
            // 直接請求 JSON 並反序列化為臨時的 DTO 結構
            var response = await _httpClient.GetFromJsonAsync<HkoWeatherResponse>(ApiUrl);
            
            if (response == null) return null;

            var weatherData = new WeatherData();

            // 1. 提取更新時間 (格式化為 HH:mm)
            if (DateTime.TryParse(response.UpdateTime, out var dt))
            {
                weatherData.UpdateTime = dt.ToString("HH:mm");
            }
            else
            {
                weatherData.UpdateTime = response.UpdateTime;
            }

            // 2. 提取總體濕度
            weatherData.Humidity = response.Humidity?.Data?.FirstOrDefault()?.Value.ToString() ?? "--";

            // 3. 提取總體氣溫與地區氣溫
            if (response.Temperature?.Data != null)
            {
                foreach (var item in response.Temperature.Data)
                {
                    // 移除名稱中可能存在的空格
                    string cleanName = item.Place.Replace(" ", "").Trim();
                    string tempVal = item.Value.ToString();

                    // 設定總體氣溫 (以香港天文台為準)
                    if (cleanName == "香港天文台")
                    {
                        weatherData.CurrentTemp = tempVal;
                    }

                    // 加入地區清單
                    weatherData.Districts.Add(new DistrictTemperature(cleanName, tempVal));
                }
            }

            // 如果沒抓到天文台氣溫，拿第一個地區的湊合一下
            if (weatherData.CurrentTemp == "--" && weatherData.Districts.Any())
            {
                weatherData.CurrentTemp = weatherData.Districts.First().Temperature;
            }

            weatherData.LastFetched = DateTime.UtcNow;
            return weatherData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch HKO JSON Weather API");
            return null;
        }
    }

    // --- 內部 JSON 對應類別 (DTOs) ---
    private class HkoWeatherResponse
    {
        [JsonPropertyName("updateTime")]
        public string UpdateTime { get; set; } = string.Empty;

        [JsonPropertyName("temperature")]
        public TemperatureContainer? Temperature { get; set; }

        [JsonPropertyName("humidity")]
        public HumidityContainer? Humidity { get; set; }
    }

    private class TemperatureContainer
    {
        [JsonPropertyName("data")]
        public List<TemperatureItem>? Data { get; set; }
    }

    private class TemperatureItem
    {
        [JsonPropertyName("place")]
        public string Place { get; set; } = string.Empty;
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    private class HumidityContainer
    {
        [JsonPropertyName("data")]
        public List<HumidityItem>? Data { get; set; }
    }

    private class HumidityItem
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }
}