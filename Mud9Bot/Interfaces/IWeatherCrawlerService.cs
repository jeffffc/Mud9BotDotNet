using Mud9Bot.Models;

namespace Mud9Bot.Interfaces;

public interface IWeatherCrawlerService
{
    Task<WeatherData?> FetchWeatherAsync();
    Task<WeatherForecast?> FetchForecastAsync();
}