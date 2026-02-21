using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class GreetingService(IServiceScopeFactory scopeFactory, ILogger<GreetingService> logger) : IGreetingService
{
    // High-speed RAM cache. Key: (TelegramId, GreetingType), Value: List of messages
    private ConcurrentDictionary<(long, string), List<string>> _cache = new();

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            // Fetch everything from the DB
            var allGreetings = await db.Set<CustomGreeting>().ToListAsync();

            var newCache = new ConcurrentDictionary<(long, string), List<string>>();

            // Group by User ID and Greeting Type, and store the text contents
            foreach (var group in allGreetings.GroupBy(g => (g.TelegramId, g.GreetingType.ToUpper())))
            {
                newCache[group.Key] = group.Select(g => g.Content).ToList();
            }

            _cache = newCache;
            logger.LogInformation("Greeting RAM cache primed. Loaded {Count} user-type combinations.", _cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Greeting RAM cache from Database.");
        }
    }

    public string? GetRandomGreeting(long userId, string greetingType)
    {
        var key = (userId, greetingType.ToUpper());

        if (_cache.TryGetValue(key, out var messages) && messages.Any())
        {
            var rng = new Random();
            return messages[rng.Next(messages.Count)];
        }

        return null;
    }
}