using Mud9Bot.Models;

namespace Mud9Bot.Services;

public interface IWeatherService
{
    void Update(WeatherData data);
    WeatherData? GetCurrent();
}

public class WeatherService : IWeatherService
{
    private WeatherData? _currentWeather;

    public void Update(WeatherData data) => _currentWeather = data;

    public WeatherData? GetCurrent() => _currentWeather;
}