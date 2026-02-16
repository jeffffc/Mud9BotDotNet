using System.Text.Json;
using Mud9Bot.Services.Interfaces;

namespace Mud9Bot.Services;

public class FortuneService : IFortuneService
{
    private readonly List<FortuneItem> _fortunes;
    private readonly Random _random = new();

    public FortuneService(ILogger<FortuneService> logger)
    {
        var baseDir = AppContext.BaseDirectory; 
        var filePath = Path.Combine(baseDir, "Static", "fortunelist.json");

        logger.LogInformation("Attempting to load fortunes from: {Path}", filePath);

        if (File.Exists(filePath))
        {
            try 
            {
                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                
                try 
                {
                    // Attempt 1: Dictionary format (e.g. "10": { "a": "...", "b": "..." })
                    // This matches your provided JSON structure
                    var dict = JsonSerializer.Deserialize<Dictionary<string, FortuneItem>>(json, options);
                    _fortunes = dict?.Values.ToList() ?? [];
                    logger.LogInformation("Loaded format: Dictionary<string, FortuneItem>");
                }
                catch (JsonException)
                {
                    try
                    {
                        // Attempt 2: List of Objects (e.g. [{ "a": "...", "b": "..." }])
                        _fortunes = JsonSerializer.Deserialize<List<FortuneItem>>(json, options) ?? [];
                        logger.LogInformation("Loaded format: List<FortuneItem>");
                    }
                    catch (JsonException)
                    {
                        // Attempt 3: Legacy List of Strings (e.g. ["fortune1", "fortune2"])
                        var legacyList = JsonSerializer.Deserialize<List<string>>(json, options);
                        if (legacyList != null)
                        {
                            // "暫無詳情" = No details available
                            _fortunes = legacyList.Select(s => new FortuneItem(s, "暫無詳情")).ToList();
                            logger.LogInformation("Loaded format: List<string>");
                        }
                        else
                        {
                            _fortunes = [];
                        }
                    }
                }
                
                logger.LogInformation("Successfully loaded {Count} fortunes.", _fortunes.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse fortune JSON.");
                _fortunes = [];
            }
        }
        else
        {
            logger.LogWarning("Fortune file NOT found at: {Path}", filePath);
            _fortunes = [];
        }
    }

    public (FortuneItem Item, int Index) GetRandomFortune()
    {
        if (_fortunes.Count == 0) 
        {
            // "未能讀取靈籤資料" = Unable to read fortune data
            // "請聯絡管理員檢查系統日誌" = Please contact admin to check system logs
            return (new FortuneItem("未能讀取靈籤資料。", "請聯絡管理員檢查系統日誌 (Path/Format Error)。"), -1);
        }
        
        var index = _random.Next(_fortunes.Count);
        return (_fortunes[index], index);
    }

    public FortuneItem? GetFortuneByIndex(int index)
    {
        if (index >= 0 && index < _fortunes.Count)
        {
            return _fortunes[index];
        }
        return null;
    }
}