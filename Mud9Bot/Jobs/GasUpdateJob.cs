using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Jobs;

// 定義：啟動時執行一次，且每晚凌晨 1 點更新
[QuartzJob(Name = "GasUpdateJob", CronInterval = "0 0 1 * * ?", RunOnStartup = true, Description = "Fetch latest HK gas prices from Consumer Council")]
public class GasUpdateJob(IGasService gasService, ILogger<GasUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Gas Update Job 執行中...");
        await gasService.UpdatePricesAsync(context.CancellationToken);
    }
}