namespace Mud9Bot.Interfaces;

public interface IGreetingService
{
    /// <summary>
    /// Loads all custom greetings from the database into RAM cache.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Retrieves a random greeting string for a specific user and greeting type from RAM.
    /// Returns null if no greetings are configured.
    /// </summary>
    string? GetRandomGreeting(long userId, string greetingType);
}