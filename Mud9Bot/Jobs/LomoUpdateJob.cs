using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;

namespace Mud9Bot.Jobs;

// Runs on startup, and automatically refreshes once a day at 5 AM as a fallback
[QuartzJob(Name = "LomoUpdateJob", CronInterval = "0 0 5 * * ?", RunOnStartup = true, Description = "Load Lomo Ignore Words into RAM cache")]
public class LomoUpdateJob(ILomoService lomoService, ILogger<LomoUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        bool isStartup = context.Trigger.Key.Name.Contains("startup");
        logger.LogInformation("Lomo Update Job triggered. Mode: {Mode}", isStartup ? "Startup" : "Scheduled");
        
        await lomoService.InitializeAsync();
    }
}