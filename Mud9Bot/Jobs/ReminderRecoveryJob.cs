using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "ReminderRecoveryJob", RunOnStartup = true, Inactive = false, Description = "Startup task to reload pending jobs")]
public class ReminderRecoveryJob(IReminderService reminderService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // 只有在 Startup Trigger 觸發時才執行恢復邏輯
        if (context.Trigger.Key.Name.Contains("startup"))
        {
            await reminderService.RecoverPendingRemindersAsync();
        }
    }
}