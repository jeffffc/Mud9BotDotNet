using Mud9Bot.Models;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;


public class WeatherService : IWeatherService
{
    private WeatherData? _currentWeather;

    public void Update(WeatherData data) => _currentWeather = data;

    public WeatherData? GetCurrent() => _currentWeather;
}