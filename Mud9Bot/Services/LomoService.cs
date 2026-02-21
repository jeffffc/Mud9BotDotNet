using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class LomoService(IServiceScopeFactory scopeFactory, ILogger<LomoService> logger) : ILomoService
{
    private HashSet<string> _cache = new();

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            var words = await db.Set<LomoIgnoreWord>().Select(w => w.Word).ToListAsync();
            
            // Replace the cache entirely for thread-safety during refresh
            _cache = new HashSet<string>(words);
            
            logger.LogInformation("Lomo ignore list loaded. {Count} words cached in RAM.", _cache.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load Lomo ignore list.");
        }
    }

    public HashSet<string> GetIgnoreWords() => _cache;

    public async Task<bool> AddWordAsync(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // Check if it already exists
        if (await db.Set<LomoIgnoreWord>().AnyAsync(w => w.Word == word)) return true;

        db.Set<LomoIgnoreWord>().Add(new LomoIgnoreWord { Word = word });
        await db.SaveChangesAsync();

        // Trigger a full cache refresh
        await InitializeAsync();
        return true;
    }

    public async Task<bool> RemoveWordAsync(string word)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var entity = await db.Set<LomoIgnoreWord>().FirstOrDefaultAsync(w => w.Word == word);
        if (entity == null) return false;

        db.Set<LomoIgnoreWord>().Remove(entity);
        await db.SaveChangesAsync();

        // Trigger a full cache refresh
        await InitializeAsync();
        return true;
    }
}