using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Extensions; // 假設 ToHkTime 在這裡
using Quartz;

namespace Mud9Bot.Jobs;

// 注意：如果伺服器時區是 UTC，此 Cron 會在香港時間 08:00 執行。
// 若要精準香港午夜執行，建議在 Quartz 啟動設定中指定 TimeZone，或調整 Cron。
[QuartzJob(Name = "QuotaResetJob", CronInterval = "0 0 0 * * ?", Description = "Reset Wine/Plastic Quota every midnight")]
public class QuotaResetJob(IServiceScopeFactory scopeFactory, ILogger<QuotaResetJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // 使用 UtcNow 並轉換顯示，確保 Log 看到的資訊跟用戶認知的一致
        var nowHk = DateTime.UtcNow.ToHkTime();
        logger.LogInformation("Starting Daily Quota Reset Job at HK Time: {Time}", nowHk);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // 純 SQL 更新，不涉及 C# DateTime 傳參，因此不會有時區 Kind 報錯問題
        var sql = @"
            UPDATE dailylimit d
            SET wlimit = g.wquota,
                plimit = g.pquota
            FROM groups g
            WHERE d.groupid = g.groupid;
        ";

        try 
        {
            var rowsAffected = await db.Database.ExecuteSqlRawAsync(sql);
            logger.LogInformation("Daily Quota Reset Complete. Updated {Count} rows.", rowsAffected);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset daily quotas in PostgreSQL.");
        }
    }
}