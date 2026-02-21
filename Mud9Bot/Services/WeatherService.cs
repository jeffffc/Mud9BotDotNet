using Mud9Bot.Models;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;


public class WeatherService : IWeatherService
{
    private WeatherData? _currentWeather;
    private WeatherForecast? _currentForecast;

    public void Update(WeatherData data) => _currentWeather = data;
    public void UpdateForecast(WeatherForecast data) => _currentForecast = data;

    public WeatherData? GetCurrent() => _currentWeather;
    public WeatherForecast? GetForecast() => _currentForecast;
}