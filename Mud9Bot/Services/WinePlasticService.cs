using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot.Extensions;

namespace Mud9Bot.Services;

public class WinePlasticService(BotDbContext context, ILogger<WinePlasticService> logger) : IWinePlasticService
{
    public async Task<(int WineLeft, int PlasticLeft)> GetOrCreateQuotaAsync(int userId, int groupId)
    {
        var limit = await context.Set<DailyLimit>()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.GroupId == groupId);

        if (limit == null)
        {
            limit = await CreateDefaultLimitAsync(userId, groupId);
        }

        return (limit.WineLimit, limit.PlasticLimit);
    }
    
    public async Task<(bool, string)> ProcessTransactionAsync(int senderId, int targetId, int groupId, bool isWine, int num)
    {
        // 1. Get Sender Quota
        var limit = await context.Set<DailyLimit>()
            .FirstOrDefaultAsync(d => d.UserId == senderId && d.GroupId == groupId);

        if (limit == null)
        {
            // Create on the fly if missing
            limit = await CreateDefaultLimitAsync(senderId, groupId);
        }

        // 2. Check Quota
        if (isWine && limit.WineLimit < num) return (false, $"你今日得返 <code>{limit.WineLimit}</code> 杯酒，點一次過賜 <code>{num}</code> 杯酒俾人呀？");
        if (!isWine && limit.PlasticLimit < num) return (false, $"你今日得番 <code>{limit.PlasticLimit}</code> 粒膠， 點一次過派 <code>{num}</code> 粒膠俾人呀？");

        // 3. Deduct Quota
        if (isWine) limit.WineLimit -= num;
        else limit.PlasticLimit -= num;

        // 4. Create Transaction Record
        var record = new WinePlastic
        {
            GroupId = groupId,
            UserId = targetId,   // Receiver
            GivenBy = senderId,  // Sender
            Wine = isWine ? num : 0,
            Plastic = isWine ? 0 : num,
            TimeAdded = DateTime.UtcNow,
            Disabled = 0
        };
        context.Set<WinePlastic>().Add(record);

        // 5. Update Target User Stats
        var targetUser = await context.Set<BotUser>().FindAsync(targetId);
        if (targetUser != null)
        {
            if (isWine) targetUser.Wine += num;
            else targetUser.Plastic += num;
        }

        await context.SaveChangesAsync();

        var targetName = targetUser?.FirstName.EscapeHtml();

        // 6. Return Result String
        return (true, (isWine
            ? (num > 1 ? $"已對<code>【{targetName}】</code>賜 <code>{num}</code> 杯酒 🍻！" : $"已對<code>【{targetName}】</code> 賜酒 🍻！")
            : (num > 1 ? $"已對<code>【{targetName}】</code>派 <code>{num}</code> 粒膠 🌚！" : $"已對<code>【{targetName}】</code> 派膠 🌚！")
            )
            );
    }
    
    public async Task<(bool, string)> ProcessTransactionByTelegramIdAsync(long senderTgId, long targetTgId, long groupTgId, bool isWine, int num)
    {
        var senderEntity = await context.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == senderTgId);
        var targetEntity = await context.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == targetTgId);
        var groupEntity = await context.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == groupTgId);

        if (senderEntity == null || targetEntity == null || groupEntity == null)
        {
            return (false, "系統錯誤: 找不到用戶資料 (可能未同步)。");
        }

        return await ProcessTransactionAsync(senderEntity.Id, targetEntity.Id, groupEntity.Id, isWine, num);
    }
    
    public async Task<int> ResetDailyQuotasAsync()
    {
        // PostgreSQL specific syntax
        var sql = @"
            UPDATE dailylimit d
            SET wlimit = g.wquota,
                plimit = g.pquota
            FROM groups g
            WHERE d.groupid = g.groupid;
        ";
        
        return await context.Database.ExecuteSqlRawAsync(sql);
    }
    
    private async Task<DailyLimit> CreateDefaultLimitAsync(int userId, int groupId)
    {
        // Fetch group settings to determine quota
        var group = await context.Set<BotGroup>().FindAsync(groupId);
        
        var wLimit = group?.WQuota ?? Constants.WineLimit;
        var pLimit = group?.PQuota ?? Constants.PlasticLimit;

        var limit = new DailyLimit
        {
            UserId = userId,
            GroupId = groupId,
            WineLimit = wLimit,
            PlasticLimit = pLimit
        };
        
        context.Set<DailyLimit>().Add(limit);
        // Note: We don't SaveChanges here if called from ProcessTransactionAsync as it saves later,
        // but for safety in GetOrCreateQuotaAsync we might need to. 
        // However, EF Core tracks the entity in the ChangeTracker, so checking Local or database is fine.
        // For simplicity, we assume the caller will SaveChanges or the flow continues.
        // But since GetOrCreateQuotaAsync is usually read-only flow, we save here.
        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        return limit;
    }
    
    // NEW: Implementation for /check
    public async Task<(int WineCount, int PlasticCount, int WineLimit, int PlasticLimit)> GetPersonalStatsAsync(long telegramUserId, long telegramGroupId)
    {
        // 1. Get User and Group Entities
        var user = await context.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == telegramUserId);
        var group = await context.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == telegramGroupId);

        if (user == null || group == null)
        {
            // If user or group doesn't exist yet, return defaults
            return (0, 0, Constants.WineLimit, Constants.PlasticLimit);
        }

        // 2. Calculate Group-Specific Stats from WinePlastic table
        // Query equivalent to: SELECT SUM(wine), SUM(plastic) FROM wineplastic WHERE userid=... AND groupid=... AND disabled!=1
        var wineCount = await context.Set<WinePlastic>()
            .Where(wp => wp.UserId == user.Id && wp.GroupId == group.Id && wp.Disabled != 1)
            .SumAsync(wp => wp.Wine);

        var plasticCount = await context.Set<WinePlastic>()
            .Where(wp => wp.UserId == user.Id && wp.GroupId == group.Id && wp.Disabled != 1)
            .SumAsync(wp => wp.Plastic);

        // 3. Get Daily Limit
        var limit = await context.Set<DailyLimit>()
            .FirstOrDefaultAsync(d => d.UserId == user.Id && d.GroupId == group.Id);

        // If limit row exists, use it. If not, fallback to Group defaults (WQuota/PQuota).
        // Note: Python code creates the row here if missing, but for a read-only Check command, 
        // returning the effective default values is cleaner and faster.
        var wLimit = limit?.WineLimit ?? group.WQuota;
        var pLimit = limit?.PlasticLimit ?? group.PQuota;

        return (wineCount, plasticCount, wLimit, pLimit);
    }
}