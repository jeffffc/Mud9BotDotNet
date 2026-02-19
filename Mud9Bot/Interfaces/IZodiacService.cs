using Mud9Bot.Services;

namespace Mud9Bot.Interfaces;

public record ZodiacFortune(string Text, int Score = 0);

public interface IZodiacService
{
    // High-speed RAM access for the UI
    string GetSummary(int zodiacIndex);
    ZodiacFortune GetDetail(int zodiacIndex, string type);

    // Persistence and Cache Refresh
    Task UpdateDataAsync(string dateKey, Dictionary<int, ZodiacScrapeResult> newData);
    
    // Primes the RAM cache from the Database (called on startup)
    Task InitializeAsync();
}