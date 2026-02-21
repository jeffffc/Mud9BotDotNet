using Mud9Bot.Models;

namespace Mud9Bot.Interfaces;

public interface IWeatherService
{
    void Update(WeatherData data);
    void UpdateForecast(WeatherForecast data);
    WeatherData? GetCurrent();
    WeatherForecast? GetForecast();
}