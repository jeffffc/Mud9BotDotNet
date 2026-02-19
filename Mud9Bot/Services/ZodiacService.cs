using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mud9Bot.Interfaces;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
using System.Collections.Concurrent;

namespace Mud9Bot.Services;

public class ZodiacService : IZodiacService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ZodiacService> _logger;

    // Fast In-Memory Cache (Key: yyyy-MM-dd)
    private readonly ConcurrentDictionary<string, Dictionary<int, ZodiacScrapeResult>> _cache = new();

    public ZodiacService(IServiceScopeFactory scopeFactory, ILogger<ZodiacService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private string GetTodayKey() => DateTime.UtcNow.ToHkTime().ToString("yyyy-MM-dd");

    public async Task InitializeAsync()
    {
        try
        {
            var today = GetTodayKey();
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            var entities = await db.Set<ZodiacEntity>()
                .Where(z => z.DateKey == today)
                .ToListAsync();

            if (entities.Any())
            {
                var dayData = entities.ToDictionary(
                    e => e.ZodiacIndex,
                    e => new ZodiacScrapeResult(e.Summary, new Dictionary<string, (int, string)>
                    {
                        { "overall", (e.OverallScore, e.OverallText) },
                        { "love",    (e.LoveScore, e.LoveText) },
                        { "career",  (e.CareerScore, e.CareerText) },
                        { "money",   (e.MoneyScore, e.MoneyText) }
                    })
                );
                _cache[today] = dayData;
                _logger.LogInformation("Zodiac RAM cache primed for {Date}", today);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Zodiac RAM cache from Database.");
        }
    }

    public string GetSummary(int zodiacIndex)
    {
        var today = GetTodayKey();
        if (_cache.TryGetValue(today, out var dayData) && dayData.TryGetValue(zodiacIndex, out var result))
        {
            return result.Summary;
        }
        return "⚠️ 暫無今日摘要資料，請管理員檢查更新。";
    }

    public ZodiacFortune GetDetail(int zodiacIndex, string type)
    {
        var today = GetTodayKey();
        if (_cache.TryGetValue(today, out var dayData) && dayData.TryGetValue(zodiacIndex, out var result))
        {
            if (result.Categories.TryGetValue(type, out var cat))
            {
                return new ZodiacFortune(cat.Text, cat.Score);
            }
        }
        return new ZodiacFortune("暫無該項運勢詳細資料。");
    }

    public async Task UpdateDataAsync(string dateKey, Dictionary<int, ZodiacScrapeResult> newData)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // 1. Transactional Update in Postgres
        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // Remove old entries for this date if any
            var existing = await db.Set<ZodiacEntity>().Where(z => z.DateKey == dateKey).ToListAsync();
            db.Set<ZodiacEntity>().RemoveRange(existing);

            foreach (var kvp in newData)
            {
                var res = kvp.Value;
                db.Set<ZodiacEntity>().Add(new ZodiacEntity
                {
                    DateKey = dateKey,
                    ZodiacIndex = kvp.Key,
                    Summary = res.Summary,
                    OverallScore = res.Categories["overall"].Score,
                    OverallText = res.Categories["overall"].Text,
                    LoveScore = res.Categories["love"].Score,
                    LoveText = res.Categories["love"].Text,
                    CareerScore = res.Categories["career"].Score,
                    CareerText = res.Categories["career"].Text,
                    MoneyScore = res.Categories["money"].Score,
                    MoneyText = res.Categories["money"].Text
                });
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            // 2. Update RAM cache if it's for today (or future)
            _cache[dateKey] = newData;
            _logger.LogInformation("Zodiac data successfully persisted to Database and RAM for {Date}", dateKey);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to save Zodiac data to Database.");
            throw;
        }
    }
}