namespace Mud9Bot.Models;

public class MarkSixResult
{
    public string Period { get; set; } = string.Empty;
    public List<string> Numbers { get; set; } = new();
    public string SpecialBall { get; set; } = string.Empty;
    public List<string> Prizes { get; set; } = new();
    public string NextDrawTime { get; set; } = string.Empty;
    public string NextJackpot { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}