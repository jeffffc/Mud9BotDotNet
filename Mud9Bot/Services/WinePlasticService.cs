using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Services.Interfaces;

namespace Mud9Bot.Services;

public class WinePlasticService(BotDbContext context, ILogger<WinePlasticService> logger) : IWinePlasticService
{
    public async Task<(int WineLeft, int PlasticLeft)> GetOrCreateQuotaAsync(int userId, int groupId, int defaultW, int defaultP)
    {
        var limit = await context.Set<DailyLimit>()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.GroupId == groupId);

        if (limit == null)
        {
            limit = new DailyLimit
            {
                UserId = userId,
                GroupId = groupId,
                WineLimit = defaultW,
                PlasticLimit = defaultP
            };
            context.Set<DailyLimit>().Add(limit);
            await context.SaveChangesAsync();
        }

        return (limit.WineLimit, limit.PlasticLimit);
    }

    public async Task<string> ProcessTransactionAsync(int senderId, int targetId, int groupId, bool isWine, int defaultW, int defaultP)
    {
        // 1. Get Sender Quota
        var limit = await context.Set<DailyLimit>()
            .FirstOrDefaultAsync(d => d.UserId == senderId && d.GroupId == groupId);

        if (limit == null)
        {
            // Create on the fly if missing
            limit = new DailyLimit
            {
                UserId = senderId,
                GroupId = groupId,
                WineLimit = defaultW,
                PlasticLimit = defaultP
            };
            context.Set<DailyLimit>().Add(limit);
        }

        // 2. Check Quota
        if (isWine && limit.WineLimit <= 0) return "今日酒奎額已用完 (No Wine Quota left).";
        if (!isWine && limit.PlasticLimit <= 0) return "今日膠奎額已用完 (No Plastic Quota left).";

        // 3. Deduct Quota
        if (isWine) limit.WineLimit--;
        else limit.PlasticLimit--;

        // 4. Create Transaction Record
        var record = new WinePlastic
        {
            GroupId = groupId,
            UserId = targetId,   // Receiver
            GivenBy = senderId,  // Sender
            Wine = isWine ? 1 : 0,
            Plastic = isWine ? 0 : 1,
            TimeAdded = DateTime.UtcNow,
            Disabled = 0
        };
        context.Set<WinePlastic>().Add(record);

        // 5. Update Target User Stats
        var targetUser = await context.Set<BotUser>().FindAsync(targetId);
        if (targetUser != null)
        {
            if (isWine) targetUser.Wine++;
            else targetUser.Plastic++;
        }

        await context.SaveChangesAsync();

        // 6. Return Result String
        var icon = isWine ? "🍷" : "💊";
        var action = isWine ? "派酒" : "派膠";
        var totalStats = targetUser != null ? (isWine ? targetUser.Wine : targetUser.Plastic) : 0;
        var leftQuota = isWine ? limit.WineLimit : limit.PlasticLimit;

        return $"{action}成功 {icon}！\n對方累積: {totalStats}\n你今日仲有 {leftQuota} 個奎額。";
    }
}