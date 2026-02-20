using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Mud9Bot.Interfaces;
using System.Collections.Concurrent;

namespace Mud9Bot.Services;

public class TranslateService : ITranslateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranslateService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://translation.googleapis.com/language/translate/v2";

    // Track rolling usage in memory: Key is UserId, Value is list of request timestamps
    private readonly ConcurrentDictionary<long, List<DateTime>> _usageHistory = new();

    public TranslateService(HttpClient httpClient, ILogger<TranslateService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["GoogleCloud:ApiKey"] ?? "";
    }

    public async Task<TranslationResult> TranslateAsync(long userId, string text, string targetLanguage = "zh-TW", CancellationToken ct = default)
    {
        // --- Rolling Rate Limit Logic ---
        var now = DateTime.UtcNow;
        var history = _usageHistory.GetOrAdd(userId, _ => new List<DateTime>());

        lock (history)
        {
            // 1. Remove timestamps older than 60 seconds
            history.RemoveAll(t => t < now.AddSeconds(-60));

            // 2. Check if user exceeded the limit (10 translations per 60s)
            if (history.Count >= 10)
            {
                _logger.LogWarning("User {UserId} rate limited for translation.", userId);
                return new TranslationResult("⚠️ 你用得太快喇，休息下先啦（每分鐘限制 10 次）。", "limit");
            }

            // 3. Record this request
            history.Add(now);
        }

        // --- Standard API Call Logic ---
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new TranslationResult("❌ Error: API Key missing", "err");
        }

        var url = $"{BaseUrl}?key={_apiKey}";
        var payload = new { q = text, target = targetLanguage };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        int retryCount = 0;
        int maxRetries = 5;

        while (retryCount <= maxRetries)
        {
            try
            {
                var response = await _httpClient.PostAsync(url, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(responseBody);
                    
                    var translation = doc.RootElement
                        .GetProperty("data")
                        .GetProperty("translations")[0];

                    var translatedText = translation.GetProperty("translatedText").GetString() ?? "";
                    var detectedSrc = translation.GetProperty("detectedSourceLanguage").GetString() ?? "unknown";

                    return new TranslationResult(WebUtility.HtmlDecode(translatedText), detectedSrc);
                }

                if (retryCount < maxRetries)
                {
                    await Task.Delay((int)Math.Pow(2, retryCount) * 1000, ct);
                    retryCount++;
                    continue;
                }

                return new TranslationResult($"❌ Translation failed: {response.StatusCode}", "err");
            }
            catch (Exception ex)
            {
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Google Translation API failed after retries.");
                    return new TranslationResult($"❌ Network Error: {ex.Message}", "err");
                }
                await Task.Delay((int)Math.Pow(2, retryCount) * 1000, ct);
                retryCount++;
            }
        }

        return new TranslationResult("❌ Unexpected error", "err");
    }
}