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
    private readonly ConcurrentDictionary<(string type, string meta, string chat), long> _buffer = new();
    
    // 🚀 除錯模式：維持 1，直到確認資料庫寫入正常
    private const int FlushThreshold = 50; 

    public async Task RecordUpdateAsync(Update update, CancellationToken ct)
    {
        await RecordEventAsync("system", "total_volume", update, ct);
    }

    public async Task RecordEventAsync(string eventType, string? metadata, Update update, CancellationToken ct)
    {
        var chatType = GetChatType(update);
        var key = (eventType, metadata ?? "none", chatType);

        _buffer.AddOrUpdate(key, 1, (_, val) => val + 1);

        if (_buffer.Values.Sum() >= FlushThreshold)
        {
            // 在除錯階段使用 await 確保錯誤被捕捉
            await FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        if (_buffer.IsEmpty) return;

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

                // 🚀 關鍵修正：對所有識別字加上雙引號 "" 以免 PostgreSQL 大小寫解析出錯
                // 同時確保 ON CONFLICT 的欄位名稱與資料庫內的 Unique Index 完全一致
                var sql = @"
                    INSERT INTO ""bot_event_logs"" (""event_type"", ""metadata"", ""chat_type"", ""count"")
                    VALUES ({0}, {1}, {2}, {3})
                    ON CONFLICT (""event_type"", ""metadata"", ""chat_type"") 
                    DO UPDATE SET ""count"" = ""bot_event_logs"".""count"" + EXCLUDED.""count"";";

                await db.Database.ExecuteSqlRawAsync(sql, type, meta, chat, count);
            }
            
            logger.LogDebug("Successfully flushed {Count} stats to bot_event_logs.", snapshot.Length);
        }
        catch (Exception ex)
        {
            // 這裡會詳細記錄是哪個欄位出問題
            logger.LogCritical(ex, "FATAL BotStats Flush Error: {Message}", ex.Message);
        }
    }

    private string GetChatType(Update update)
    {
        var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat ?? update.ChannelPost?.Chat;
        return chat?.Type.ToString().ToLower() ?? "unknown";
    }
}