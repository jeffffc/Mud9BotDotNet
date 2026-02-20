using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Services;

public interface IMovieService
{
    List<Movie> GetCachedMovies();
    Task UpdateMoviesAsync(List<Movie> scrapedMovies);
    Task InitializeAsync();
}

public class MovieService(IServiceScopeFactory scopeFactory, ILogger<MovieService> logger) : IMovieService
{
    private List<Movie> _cache = new();

    public List<Movie> GetCachedMovies() => _cache;

    public async Task InitializeAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        
        // 啟動時載入標記為正在上映的電影
        _cache = await db.Set<Movie>()
            .Where(m => m.IsShowing)
            .OrderByDescending(m => m.LastUpdated)
            .ToListAsync();
            
        logger.LogInformation("Movie RAM cache primed with {Count} active movies.", _cache.Count);
    }

    public async Task UpdateMoviesAsync(List<Movie> scrapedMovies)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // 1. 先將所有電影設為非上映狀態
            var allMovies = await db.Set<Movie>().ToListAsync();
            foreach (var m in allMovies) m.IsShowing = false;

            var activeMovies = new List<Movie>();

            // 2. 執行 Upsert (更新或插入)
            foreach (var scraped in scrapedMovies)
            {
                var existing = await db.Set<Movie>().FirstOrDefaultAsync(m => m.Link == scraped.Link);
                
                if (existing != null)
                {
                    // 修正：補上漏掉的 OnShowDate 賦值
                    existing.Title = scraped.Title;
                    existing.Rating = scraped.Rating;
                    existing.Description = scraped.Description;
                    existing.Genre = scraped.Genre;
                    existing.Writer = scraped.Writer;
                    existing.Director = scraped.Director;
                    existing.Starring = scraped.Starring;
                    existing.Length = scraped.Length;
                    existing.Grade = scraped.Grade;
                    existing.Language = scraped.Language;
                    existing.OnShowDate = scraped.OnShowDate; // <--- 補回這一行
                    
                    existing.IsShowing = true; 
                    existing.LastUpdated = DateTime.UtcNow;
                    
                    activeMovies.Add(existing);
                }
                else
                {
                    scraped.IsShowing = true;
                    scraped.LastUpdated = DateTime.UtcNow;
                    db.Set<Movie>().Add(scraped);
                    
                    activeMovies.Add(scraped);
                }
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            // 3. 更新記憶體快取
            _cache = activeMovies;
            logger.LogInformation("Movie DB upsert complete. Updated {Count} active movies.", activeMovies.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to upsert movies in database.");
        }
    }
}