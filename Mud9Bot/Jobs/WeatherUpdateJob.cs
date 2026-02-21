using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Quartz;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "WeatherUpdateJob", CronInterval = "0 5 * * * ?", RunOnStartup = true, Description = "Hourly weather and forecast update")]
public class WeatherUpdateJob(IWeatherService weatherService, IWeatherCrawlerService crawler, ILogger<WeatherUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Weather Update Job started...");
        
        // 1. 更新現時天氣
        var data = await crawler.FetchWeatherAsync();
        if (data != null)
        {
            weatherService.Update(data);
            logger.LogInformation("Current weather updated.");
        }

        // 2. 更新天氣預報
        var forecast = await crawler.FetchForecastAsync();
        if (forecast != null)
        {
            weatherService.UpdateForecast(forecast);
            logger.LogInformation("Weather forecast updated.");
        }
    }
}