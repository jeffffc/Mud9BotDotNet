using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "GreetingUpdateJob", CronInterval = "0 0 */12 * * ?", RunOnStartup = true, Description = "Refresh custom greetings cache from DB")]
public class GreetingUpdateJob(IGreetingService greetingService, ILogger<GreetingUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Greeting Update Job starting...");
        await greetingService.InitializeAsync();
    }
}