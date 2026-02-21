using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "NewsUpdateJob", CronInterval = "0 0/15 * * * ?", RunOnStartup = true, Description = "Fetch RTHK News every 15 minutes")]
public class NewsUpdateJob(INewsService newsService, ILogger<NewsUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("News Update Job starting...");
        await newsService.UpdateAllNewsAsync(context.CancellationToken);
    }
}