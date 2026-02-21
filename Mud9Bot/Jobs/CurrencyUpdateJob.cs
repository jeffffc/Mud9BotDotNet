using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "CurrencyUpdateJob", CronInterval = "0 0 3 * * ?", RunOnStartup = true, Description = "Fetch latest currency rates from Fixer.io")]
public class CurrencyUpdateJob(ICurrencyService currencyService, ILogger<CurrencyUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        bool isStartup = context.Trigger.Key.Name.Contains("startup");
        logger.LogInformation("Currency Update Job triggered (Mode: {Mode})", isStartup ? "Startup" : "Scheduled");

        try
        {
            // Service 內部會自動判斷「是否需要抓取」還是「僅初始化快取」。
            // 第一次啟動時，它會先嘗試 Initialize (回報 0)，發現沒資料後會進行 API 抓取並存檔。
            await currencyService.UpdateRatesFromApiAsync();

            logger.LogInformation("Currency Update Job executed successfully.");
        }
        catch (Exception ex)
        {
            // 捕捉 Job 執行過程中的異常，避免 Quartz 核心出現未處理錯誤
            logger.LogError(ex, "An error occurred while executing CurrencyUpdateJob.");
        }
    }
}