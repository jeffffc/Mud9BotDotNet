using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class GroupService(IServiceScopeFactory scopeFactory, ILogger<GroupService> logger) : IGroupService
{
    // Key: TelegramId, Value: BotGroup 實體
    private readonly ConcurrentDictionary<long, BotGroup> _cache = new();

    public async Task<BotGroup?> GetGroupSettingsAsync(long telegramId, CancellationToken ct = default)
    {
        // 1. 先從 RAM 搵
        if (_cache.TryGetValue(telegramId, out var cachedGroup))
        {
            return cachedGroup;
        }

        // 2. RAM 冇就去 DB 抓
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var group = await db.Set<BotGroup>()
            .AsNoTracking() // 唯讀快取，不追蹤實體以增進效能
            .FirstOrDefaultAsync(g => g.TelegramId == telegramId, ct);

        if (group != null)
        {
            _cache.TryAdd(telegramId, group);
            logger.LogInformation("Group cache primed for: {ChatId} ({Title})", telegramId, group.Title);
        }

        return group;
    }

    public void RefreshCache(BotGroup group)
    {
        // 更新或新增快取
        _cache[group.TelegramId] = group;
        logger.LogDebug("Cache refreshed for group: {ChatId}", group.TelegramId);
    }
}