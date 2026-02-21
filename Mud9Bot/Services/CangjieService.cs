using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class CangjieService(ILogger<CangjieService> logger) : ICangjieService
{
    // 定義統一的字數限制常數
    public const int MaxInputLength = 20;

    private readonly Dictionary<string, string> _charToCodeMap = new();
    private readonly Dictionary<char, char> _cjRefMap = new();
    private bool _loaded = false;

    private class RawEntry1
    {
        [JsonPropertyName("char")] public string? Char { get; set; }
        [JsonPropertyName("cangjie")] public string? Cangjie { get; set; }
    }

    private class RawEntry2
    {
        [JsonPropertyName("char")] public string? Char { get; set; }
        [JsonPropertyName("cj")] public string? Cj { get; set; }
    }

    public async Task InitializeAsync()
    {
        if (_loaded) return;

        try
        {
            string staticPath = Path.Combine(AppContext.BaseDirectory, "Static");

            // 1. 載入字根對照 (cjref.json)
            string refPath = Path.Combine(staticPath, "cjref.json");
            if (File.Exists(refPath))
            {
                var refData = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(refPath));
                if (refData != null)
                {
                    foreach (var kvp in refData)
                    {
                        if (kvp.Key.Length > 0 && kvp.Value.Length > 0)
                            _cjRefMap[kvp.Key[0]] = kvp.Value[0];
                    }
                }
            }

            // 2. 載入檔案 1 (chinesewords.json)
            LoadFile<RawEntry1>(Path.Combine(staticPath, "chinesewords.json"), e => (e.Char, e.Cangjie));

            // 3. 載入檔案 2 (chinesewords2.json)
            LoadFile<RawEntry2>(Path.Combine(staticPath, "chinesewords2.json"), e => (e.Char, e.Cj));

            _loaded = true;
            logger.LogInformation("Cangjie data loaded. Total characters: {Count}", _charToCodeMap.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cangjie Initialization Failed.");
        }
        
        await Task.CompletedTask;
    }

    private void LoadFile<T>(string path, Func<T, (string? Char, string? Code)> selector)
    {
        if (!File.Exists(path)) return;
        var data = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path));
        if (data == null) return;

        foreach (var item in data)
        {
            var (c, code) = selector(item);
            if (!string.IsNullOrEmpty(c) && !string.IsNullOrEmpty(code))
            {
                // 如果字典已存在則跳過，確保不重複
                _charToCodeMap.TryAdd(c, code.ToUpper());
            }
        }
    }

    public (string Code, string Radicals)? GetCode(char c)
    {
        // 確保資料已載入 (類似 Python 的 open 邏輯，但只載入一次)
        if (!_loaded) InitializeAsync().Wait();

        string key = c.ToString();
        if (!_charToCodeMap.TryGetValue(key, out var code)) 
            return null;

        // 轉換字母為中文根 (例如: A -> 日)
        var radicals = new string(code.Select(ch => _cjRefMap.GetValueOrDefault(ch, ch)).ToArray());
        return (code, radicals);
    }
}