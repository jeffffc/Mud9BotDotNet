using Microsoft.Extensions.Logging;
using Mud9Bot.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class WeatherCrawlerService : IWeatherCrawlerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherCrawlerService> _logger;
    private const string ApiUrl = "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=rhrread&lang=tc";
    private const string ForecastApiUrl = "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=fnd&lang=tc";

    // 使用較寬鬆的 JSON 解析設定
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public WeatherCrawlerService(ILogger<WeatherCrawlerService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        // 加入 User-Agent 模擬瀏覽器，防止被天文台 API 攔截
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public async Task<WeatherData?> FetchWeatherAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<HkoWeatherResponse>(ApiUrl, _jsonOptions);
            if (response == null) return null;

            var weatherData = new WeatherData();

            if (DateTime.TryParse(response.UpdateTime, out var dt))
            {
                weatherData.UpdateTime = dt.ToString("HH:mm");
            }
            else
            {
                weatherData.UpdateTime = response.UpdateTime;
            }

            weatherData.Humidity = response.Humidity?.Data?.FirstOrDefault()?.Value.ToString() ?? "--";

            if (response.Temperature?.Data != null)
            {
                foreach (var item in response.Temperature.Data)
                {
                    string cleanName = item.Place.Replace(" ", "").Trim();
                    string tempVal = item.Value.ToString();

                    if (cleanName == "香港天文台")
                    {
                        weatherData.CurrentTemp = tempVal;
                    }
                    weatherData.Districts.Add(new DistrictTemperature(cleanName, tempVal));
                }
            }

            if (weatherData.CurrentTemp == "" && weatherData.Districts.Any())
            {
                weatherData.CurrentTemp = weatherData.Districts.First().Temperature;
            }

            weatherData.LastFetched = DateTime.UtcNow;
            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch HKO Current Weather JSON API");
            return null;
        }
    }

    public async Task<WeatherForecast?> FetchForecastAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<HkoForecastResponse>(ForecastApiUrl, _jsonOptions);
            if (response == null) 
            {
                _logger.LogWarning("HKO Forecast API returned empty response.");
                return null;
            }

            var forecast = new WeatherForecast
            {
                GeneralSituation = response.GeneralSituation,
                LastFetched = DateTime.UtcNow
            };

            if (response.WeatherForecastList != null)
            {
                foreach (var f in response.WeatherForecastList)
                {
                    // 格式化日期：將 20260222 格式化為 02月22日
                    string dateLabel = f.ForecastDate;
                    if (DateTime.TryParseExact(f.ForecastDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        dateLabel = dt.ToString("MM月dd日");
                    }

                    string fullDate = $"{dateLabel}({f.Week})";
                    string desc = $"{f.ForecastWeather} ({f.ForecastMinTemp.Value}-{f.ForecastMaxTemp.Value}℃)";
                    
                    forecast.DailyForecasts.Add(new DailyForecast(fullDate, desc));
                }
            }

            return forecast;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch HKO Forecast JSON API from {Url}", ForecastApiUrl);
            return null;
        }
    }

    // --- JSON DTOs ---

    private class HkoWeatherResponse
    {
        [JsonPropertyName("updateTime")] public string UpdateTime { get; set; } = string.Empty;
        [JsonPropertyName("temperature")] public TemperatureContainer? Temperature { get; set; }
        [JsonPropertyName("humidity")] public HumidityContainer? Humidity { get; set; }
    }

    private class TemperatureContainer
    {
        [JsonPropertyName("data")] public List<TemperatureItem>? Data { get; set; }
    }

    private class TemperatureItem
    {
        [JsonPropertyName("place")] public string Place { get; set; } = string.Empty;
        [JsonPropertyName("value")] public double Value { get; set; } // 改用 double 增加相容性
    }

    private class HumidityContainer
    {
        [JsonPropertyName("data")] public List<HumidityItem>? Data { get; set; }
    }

    private class HumidityItem
    {
        [JsonPropertyName("value")] public int Value { get; set; }
    }

    private class HkoForecastResponse
    {
        [JsonPropertyName("generalSituation")] public string GeneralSituation { get; set; } = string.Empty;
        [JsonPropertyName("weatherForecast")] public List<ForecastItem>? WeatherForecastList { get; set; }
    }

    private class ForecastItem
    {
        [JsonPropertyName("forecastDate")] public string ForecastDate { get; set; } = string.Empty;
        [JsonPropertyName("week")] public string Week { get; set; } = string.Empty;
        [JsonPropertyName("forecastWeather")] public string ForecastWeather { get; set; } = string.Empty;
        [JsonPropertyName("forecastMaxtemp")] public TempValue ForecastMaxTemp { get; set; } = new();
        [JsonPropertyName("forecastMintemp")] public TempValue ForecastMinTemp { get; set; } = new();
    }

    private class TempValue
    {
        [JsonPropertyName("value")] public int Value { get; set; }
    }
}