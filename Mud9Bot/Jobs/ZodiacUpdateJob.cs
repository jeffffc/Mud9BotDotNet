using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Mud9Bot.Services;
using Quartz;
using Telegram.Bot;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "ZodiacUpdateJob", CronInterval = "0 30 0 * * ?", RunOnStartup = true, Description = "Run on startup, and Update Zodiac data daily at 00:30 HK time")]
public class ZodiacUpdateJob(
    IZodiacService zodiacService, 
    IZodiacCrawlerService crawler, 
    ITelegramBotClient bot, 
    IConfiguration config,
    ILogger<ZodiacUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var adminId = config.GetValue<long>("BotSettings:AdminId");
        var today = DateTime.UtcNow.ToHkTime();
        
        logger.LogInformation("Zodiac Update Job Triggered.");

        // Fetch today and tomorrow
        string[] dates = { today.ToString("yyyy-MM-dd"), today.AddDays(1).ToString("yyyy-MM-dd") };

        foreach (var dateKey in dates)
        {
            var results = await crawler.FetchAllSignsAsync(dateKey);
            if (results.Count == 12)
            {
                await zodiacService.UpdateDataAsync(dateKey, results);
            }
            else
            {
                logger.LogWarning("Zodiac fetch for {Date} returned incomplete results ({Count}/12)", dateKey, results.Count);
            }
        }

        if (adminId != 0)
        {
            await bot.SendMessage(adminId, "✅ Daily Zodiac Data (DB) Updated.");
        }
    }
}