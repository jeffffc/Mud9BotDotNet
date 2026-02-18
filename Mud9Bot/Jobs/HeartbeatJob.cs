using Mud9Bot.Attributes;
using Quartz;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "HeartbeatJob", IntervalSeconds = 60, Description = "Logs a heartbeat every minute")]
public class HeartbeatJob(ILogger<HeartbeatJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("💓 Heartbeat Job executed at {Time}", DateTime.Now);
        return Task.CompletedTask;
    }
}