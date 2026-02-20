using Mud9Bot.Attributes;
using Mud9Bot.Services;
using Quartz;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Jobs;

// 每天凌晨 4 點更新一次攝影機清單即可，因為位置鮮少變動
[QuartzJob(Name = "TrafficUpdateJob", CronInterval = "0 0 4 * * ?", RunOnStartup = true, Description = "Fetch traffic camera locations")]
public class TrafficUpdateJob(ITrafficService trafficService, ILogger<TrafficUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var isStartup = context.Trigger.Key.Name.Contains("startup");
        logger.LogInformation("Traffic Data Job starting... (Mode: {Mode})", isStartup ? "Startup" : "Scheduled");
        
        int retryCount = 0;
        const int maxRetries = 3;
        bool success = false;

        while (retryCount < maxRetries && !success)
        {
            try
            {
                // 如果是手動觸發或啟動觸發，延遲幾秒等待網路穩定 (針對 Docker 環境)
                if (isStartup && retryCount == 0) await Task.Delay(3000);

                await trafficService.InitializeAsync();
                
                var regions = trafficService.GetRegions();
                if (regions != null && regions.Any())
                {
                    logger.LogInformation("Traffic Snapshot data initialized successfully with {Count} regions.", regions.Count);
                    success = true;
                }
                else
                {
                    retryCount++;
                    logger.LogWarning("Traffic Snapshot attempt {Count} resulted in empty cache.", retryCount);
                    if (retryCount < maxRetries) await Task.Delay(TimeSpan.FromSeconds(5 * retryCount)); 
                }
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogError(ex, "Exception during Traffic Snapshot initialization (Attempt {Count}/{Max})", retryCount, maxRetries);
                if (retryCount < maxRetries) await Task.Delay(TimeSpan.FromSeconds(5 * retryCount));
            }
        }

        if (!success)
        {
            logger.LogCritical("Failed to initialize Traffic Snapshot data after {Max} attempts. Check Gov API connectivity or XML structure.", maxRetries);
        }
    }
}