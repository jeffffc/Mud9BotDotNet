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