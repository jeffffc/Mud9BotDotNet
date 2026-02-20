using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Mud9Bot.Interfaces;
using System.Collections.Concurrent;

namespace Mud9Bot.Services;

public class SpeechService : ISpeechService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpeechService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://speech.googleapis.com/v1/speech:recognize";

    // 頻率限制追蹤：Key 為 UserId, Value 為請求時間戳清單
    private readonly ConcurrentDictionary<long, List<DateTime>> _usageHistory = new();

    public SpeechService(HttpClient httpClient, ILogger<SpeechService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["GoogleCloud:ApiKey"] ?? "";
    }

    public async Task<SpeechResult> RecognizeAsync(long userId, byte[] audioData, CancellationToken ct = default)
    {
        // 1. 頻率限制檢查 (60秒內最多 10 次)
        var now = DateTime.UtcNow;
        var history = _usageHistory.GetOrAdd(userId, _ => new List<DateTime>());

        lock (history)
        {
            history.RemoveAll(t => t < now.AddSeconds(-60));
            if (history.Count >= 10)
            {
                return new SpeechResult(false, "⚠️ 你用得太快喇，休息下先啦（每分鐘限制 10 次）。", "LIMIT");
            }
            history.Add(now);
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new SpeechResult(false, "❌ 系統錯誤：API Key 未設定。", "CONFIG_ERR");
        }

        // 2. 準備 Google Speech-to-Text API 請求
        // 修正：將 encoding 改回 OGG_OPUS 並指定 48000Hz 採樣率。
        // Telegram 的語音訊息 (Opus) 實際上大多採用 48000Hz 採樣率。
        var payload = new
        {
            config = new
            {
                encoding = "OGG_OPUS", 
                sampleRateHertz = 48000, 
                languageCode = "zh-HK", // 預設使用粵語
                enableAutomaticPunctuation = true
            },
            audio = new
            {
                content = Convert.ToBase64String(audioData)
            }
        };

        var url = $"{BaseUrl}?key={_apiKey}";
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // 根據您的要求：最多重試 2 次
        int retryCount = 0;
        int maxRetries = 2;

        while (retryCount <= maxRetries)
        {
            try
            {
                var response = await _httpClient.PostAsync(url, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(responseBody);
                    
                    if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var transcript = results[0]
                            .GetProperty("alternatives")[0]
                            .GetProperty("transcript")
                            .GetString();

                        return new SpeechResult(true, transcript ?? "");
                    }
                    
                    _logger.LogWarning("Speech API returned 200 but no results. Body: {Body}", responseBody);
                    return new SpeechResult(false, "我聽唔明...", "NO_SPEECH");
                }

                // --- 紀錄 API 報錯詳情以便除錯 ---
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Speech API Error {Status}: {Body}", response.StatusCode, errorBody);

                // 錯誤處理與重試邏輯 (指數退避)
                if (retryCount < maxRetries)
                {
                    await Task.Delay((int)Math.Pow(2, retryCount) * 1000, ct);
                    retryCount++;
                    continue;
                }

                // 當重試次數用盡仍失敗時，拋出 HttpRequestException。
                // 這會觸發 SpeechModule 的 catch 區塊，進而記錄 Log 並通知管理員。
                throw new HttpRequestException($"Google Speech API 請求失敗 ({response.StatusCode}): {errorBody}");
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException) throw; // 直接向上拋出已處理的 HTTP 異常

                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Google Speech API failed after {Max} retries.", maxRetries);
                    throw; 
                }
                await Task.Delay((int)Math.Pow(2, retryCount) * 1000, ct);
                retryCount++;
            }
        }

        return new SpeechResult(false, "UNKNOWN_ERROR", "FAIL");
    }
}