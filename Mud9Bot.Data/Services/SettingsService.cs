using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Data.Services;

public class SettingsService(IServiceScopeFactory scopeFactory, ILogger<SettingsService> logger) : ISettingsService
{
    private readonly ConcurrentDictionary<string, string> _settingsCache = new(StringComparer.OrdinalIgnoreCase);
    
    // 用於追蹤每個群組/用戶最後一次收到維護通知的時間 (In-Memory Only)
    private readonly ConcurrentDictionary<long, DateTime> _maintNotifyCache = new();

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            var settings = await db.Set<SystemSetting>().ToListAsync();
            
            // 全量更新快取
            _settingsCache.Clear();
            foreach (var s in settings)
            {
                _settingsCache[s.SettingKey] = s.SettingValue;
            }

            logger.LogInformation("System settings RAM cache primed. Loaded {Count} keys.", _settingsCache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize System Settings cache from database.");
        }
    }

    public bool IsMaintenanceMode()
    {
        return _settingsCache.TryGetValue("is_maintenance", out var val) && 
               val.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public string GetMaintenanceMessage()
    {
        return _settingsCache.GetValueOrDefault("maintenance_message", "🛠 系統正在維護中，請稍後再試。");
    }

    public bool ShouldNotifyMaintenance(long chatId)
    {
        var now = DateTime.UtcNow;
        
        // 嘗試獲取該 Chat 的上次通知時間
        if (_maintNotifyCache.TryGetValue(chatId, out var lastNotify))
        {
            // 如果距離上次通知不到 10 分鐘，回傳 false (不發送)
            if (now - lastNotify < TimeSpan.FromMinutes(10))
            {
                return false;
            }
        }

        // 更新通知時間並回傳 true
        _maintNotifyCache[chatId] = now;
        return true;
    }

    public string GetSetting(string key, string defaultValue = "")
    {
        return _settingsCache.GetValueOrDefault(key, defaultValue);
    }

    public void RefreshSetting(string key, string value)
    {
        _settingsCache[key] = value;
        logger.LogDebug("Setting updated in RAM: {Key}", key);
    }
}