using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Jobs;

// 定義：啟動時更新，且每晚 9點至 11點 每 5 分鐘更新一次
[QuartzJob(Name = "MarkSixUpdateJob", CronInterval = "0 0/5 21-22 * * ?", RunOnStartup = true, Description = "Scrape Mark Six results during draw evenings")]
public class MarkSixUpdateJob(IMarkSixService markSixService, ILogger<MarkSixUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Mark Six Update Job executing...");
        await markSixService.UpdateCacheAsync();
    }
}