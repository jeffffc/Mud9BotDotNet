namespace Mud9Bot.Models;

public record DistrictTemperature(string Name, string Temperature);

public class WeatherData
{
    public string CurrentTemp { get; set; } = string.Empty;
    public string Humidity { get; set; } = string.Empty;
    public string UpdateTime { get; set; } = string.Empty;
    public List<DistrictTemperature> Districts { get; set; } = new();
    public DateTime LastFetched { get; set; } = DateTime.MinValue;
}

public record DailyForecast(string Date, string Description);

public class WeatherForecast
{
    public string GeneralSituation { get; set; } = string.Empty;
    public List<DailyForecast> DailyForecasts { get; set; } = new();
    public DateTime LastFetched { get; set; } = DateTime.MinValue;
}