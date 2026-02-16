namespace Mud9Bot.Services.Interfaces;

// Define the shape of the JSON object {"a": "...", "b": "..."}
public record FortuneItem(string a, string b);

public interface IFortuneService
{
    // Returns both the item and its index (needed for the button callback)
    (FortuneItem Item, int Index) GetRandomFortune();
    
    // Validates and retrieves a specific fortune by index (for the callback)
    FortuneItem? GetFortuneByIndex(int index);
}