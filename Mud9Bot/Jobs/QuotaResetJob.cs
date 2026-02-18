using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Quartz;

namespace Mud9Bot.Jobs;

// Run at Midnight (00:00:00) every day
[QuartzJob(Name = "QuotaResetJob", CronInterval = "0 0 0 * * ?", Description = "Reset Wine/Plastic Quota every midnight")]
public class QuotaResetJob(IServiceScopeFactory scopeFactory, ILogger<QuotaResetJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Starting Daily Quota Reset Job...");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // We need to reset every user's limit to their specific group's default quota.
        // Pure SQL is most efficient here to avoid loading thousands of rows.
        
        // This query updates dailylimit table by joining with groups table
        // Syntax compatible with PostgreSQL:
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
            logger.LogInformation($"Daily Quota Reset Complete. Updated {rowsAffected} rows.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset daily quotas.");
        }
    }
}