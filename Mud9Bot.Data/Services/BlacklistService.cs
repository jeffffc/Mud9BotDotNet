using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data.Entities;
using Mud9Bot.Data.Interfaces;

namespace Mud9Bot.Data.Services;

public class BlacklistService(IServiceScopeFactory scopeFactory, ILogger<BlacklistService> logger) : IBlacklistService
{
    private HashSet<long> _cache = new();

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var ids = await db.Set<BlacklistedId>().Select(b => b.TelegramId).ToListAsync();
            _cache = new HashSet<long>(ids);
            logger.LogInformation("Blacklist RAM cache primed with {Count} IDs.", _cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Blacklist cache.");
        }
    }

    public bool IsBlacklisted(long telegramId) => _cache.Contains(telegramId);

    public async Task AddAsync(long telegramId, string reason, long adminId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        if (await db.Set<BlacklistedId>().AnyAsync(b => b.TelegramId == telegramId)) return;

        db.Set<BlacklistedId>().Add(new BlacklistedId 
        { 
            TelegramId = telegramId, 
            Reason = reason, 
            BannedBy = adminId 
        });
        await db.SaveChangesAsync();
        _cache.Add(telegramId);
    }

    public async Task RemoveAsync(long telegramId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var entry = await db.Set<BlacklistedId>().FindAsync(telegramId);
        if (entry != null)
        {
            db.Set<BlacklistedId>().Remove(entry);
            await db.SaveChangesAsync();
            _cache.Remove(telegramId);
        }
    }
}