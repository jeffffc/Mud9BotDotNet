using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;
using Telegram.Bot.Types;

namespace Mud9Bot.Services;

public class BotStatsService(IServiceScopeFactory scopeFactory, ILogger<BotStatsService> logger) : IBotStatsService
{
    // Key: (EventType, Metadata, ChatType), Value: Incremental Count
    private readonly ConcurrentDictionary<(string type, string meta, string chat), long> _buffer = new();
    
    // 🚀 除錯建議：將閾值暫時改為 1，確保每一條訊息都會立即寫入資料庫
    private const int FlushThreshold = 1; 

    public async Task RecordUpdateAsync(Update update, CancellationToken ct)
    {
        await RecordEventAsync("system", "total_volume", update, ct);
    }

    public async Task RecordEventAsync(string eventType, string? metadata, Update update, CancellationToken ct)
    {
        var chatType = GetChatType(update);
        var key = (eventType, metadata ?? "none", chatType);

        // 原子化增加 RAM 中的計數
        _buffer.AddOrUpdate(key, 1, (_, val) => val + 1);

        // 檢查是否達到寫入資料庫的門檻
        if (_buffer.Values.Sum() >= FlushThreshold)
        {
            // 使用背景任務執行，不阻塞機器人處理流程
            _ = Task.Run(() => FlushAsync(), ct);
        }
        await Task.CompletedTask;
    }

    public async Task FlushAsync()
    {
        if (_buffer.IsEmpty) return;

        // 取得快照並清空緩衝，確保執行緒安全
        var snapshot = _buffer.ToArray();
        foreach (var item in snapshot) _buffer.TryRemove(item.Key, out _);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            foreach (var item in snapshot)
            {
                var (type, meta, chat) = item.Key;
                long count = item.Value;

                // 🚀 關鍵修正：確保資料表名稱為 bot_event_logs 以對應 EF Core 實體
                // 使用 PostgreSQL 的 ON CONFLICT 語法實現高效率 UPSERT
                var sql = @"
                    INSERT INTO bot_event_logs (event_type, metadata, chat_type, count)
                    VALUES ({0}, {1}, {2}, {3})
                    ON CONFLICT (event_type, metadata, chat_type) 
                    DO UPDATE SET count = bot_event_logs.count + {3};";

                await db.Database.ExecuteSqlRawAsync(sql, type, meta, chat, count);
            }
            
            logger.LogDebug("Successfully flushed {Count} records to bot_event_logs.", snapshot.Length);
        }
        catch (Exception ex)
        {
            // 如果 SQL 報錯，會記錄在這裡
            logger.LogError(ex, "Failed to flush bot stats summary to database. Please check if table 'bot_event_logs' exists.");
        }
    }

    private string GetChatType(Update update)
    {
        var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat ?? update.ChannelPost?.Chat;
        return chat?.Type.ToString().ToLower() ?? "unknown";
    }
}