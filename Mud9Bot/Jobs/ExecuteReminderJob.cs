using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Jobs;

public class ExecuteReminderJob(
    ITelegramBotClient bot, 
    IServiceScopeFactory scopeFactory, 
    ILogger<ExecuteReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        int jobId = context.MergedJobDataMap.GetInt("jobId");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var jobRecord = await db.Set<Job>().FindAsync(jobId);
        if (jobRecord == null || jobRecord.IsProcessed) return;

        try
        {
            string msg = $"⏰ <b>Hey {jobRecord.Name}，提提你呀：</b>\n\n{jobRecord.Text}";

            await bot.SendMessage(
                chatId: jobRecord.ChatId,
                text: msg,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyParameters: new ReplyParameters { MessageId = jobRecord.MessageId }
            );

            // 標記為已執行，防止重啟時重複發送
            jobRecord.IsProcessed = true;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send reminder {Id} to Chat {ChatId}", jobId, jobRecord.ChatId);
        }
    }
}