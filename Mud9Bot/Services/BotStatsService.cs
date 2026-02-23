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
    // Memory buffer using a tuple key for grouping
    private readonly ConcurrentDictionary<(string type, string meta, string chat), long> _buffer = new();
    private const int FlushThreshold = 500;

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
            // Fire and forget background flush
            _ = Task.Run(() => FlushAsync(), ct);
        }
        await Task.CompletedTask;
    }

    public async Task FlushAsync()
    {
        if (_buffer.IsEmpty) return;

        // Take a snapshot and clear the buffer
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

                // Even with EF Code First, for high-frequency UPSERTs, 
                // raw SQL is the most efficient way to handle PostgreSQL's ON CONFLICT.
                // This updates existing rows by adding the new count or inserts a new row.
                var sql = @"
                    INSERT INTO bot_event_logs (event_type, metadata, chat_type, count)
                    VALUES ({0}, {1}, {2}, {3})
                    ON CONFLICT (event_type, metadata, chat_type) 
                    DO UPDATE SET count = bot_event_logs.count + {3};";

                await db.Database.ExecuteSqlRawAsync(sql, type, meta, chat, count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to flush aggregated bot stats to DB.");
        }
    }

    private string GetChatType(Update update)
    {
        var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat ?? update.ChannelPost?.Chat;
        return chat?.Type.ToString().ToLower() ?? "unknown";
    }
}