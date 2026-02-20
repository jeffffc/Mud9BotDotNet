using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "WeatherUpdateJob", CronInterval = "0 0 * * * ?", RunOnStartup = true, Description = "Hourly weather update")]
public class WeatherUpdateJob(IWeatherService weatherService, IWeatherCrawlerService crawler, ILogger<WeatherUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Weather Update Job started...");
        var data = await crawler.FetchWeatherAsync();
        if (data != null)
        {
            weatherService.Update(data);
            logger.LogInformation("Weather updated successfully.");
        }
    }
}