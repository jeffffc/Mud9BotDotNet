using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities.Bus;

namespace Mud9Bot.Transport.Services;

/// <summary>
/// Model for returning search results to the frontend.
/// 使用 JsonPropertyName 確保輸出同前端 JS 嘅 key 完全對應 (Case-sensitive)。
/// </summary>
public record BusRouteSearchResult(
    [property: JsonPropertyName("route")] string route, 
    [property: JsonPropertyName("bound")] string bound, 
    [property: JsonPropertyName("company")] string company, 
    [property: JsonPropertyName("dest_tc")] string dest_tc,
    [property: JsonPropertyName("orig_tc")] string orig_tc,
    [property: JsonPropertyName("service_type")] string service_type,
    [property: JsonPropertyName("type")] string type // "bus" or "minibus"
);

/// <summary>
/// High-performance Singleton cache for Bus and Minibus routes.
/// Updated with Self-Healing logic and detailed diagnostics.
/// </summary>
public class BusDirectory(IServiceScopeFactory scopeFactory, ILogger<BusDirectory> logger)
{
    private List<BusRoute> _cache = new();
    private DateTime _lastUpdated = DateTime.MinValue;

    public int GetCacheCount() => _cache.Count;
    public DateTime GetLastUpdated() => _lastUpdated;

    /// <summary>
    /// Loads all active routes into memory. 
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            logger.LogInformation("[BusDirectory] 🔄 正在從數據庫預載路綫...");

            var routes = await dbContext.Set<BusRoute>()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RouteNumber)
                .AsNoTracking()
                .ToListAsync();

            _cache = routes;
            _lastUpdated = DateTime.UtcNow;

            // Diagnostic logging to help troubleshoot "0 results" issues
            int busCount = _cache.Count(r => !r.Company.StartsWith("GMB", StringComparison.OrdinalIgnoreCase));
            int gmbCount = _cache.Count(r => r.Company.StartsWith("GMB", StringComparison.OrdinalIgnoreCase));

            logger.LogInformation("[BusDirectory] ✅ 預載完成！總數: {Total}, 巴士: {Buses}, 小巴: {Minibuses}", 
                _cache.Count, busCount, gmbCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BusDirectory] ❌ 預載路綫失敗！請檢查數據庫連線。");
        }
    }

    public async Task RefreshAsync() => await InitializeAsync();

    /// <summary>
    /// Searches the memory cache. 
    /// </summary>
    public async Task<List<BusRouteSearchResult>> SearchRoutesAsync(string query)
    {
        // SELF-HEALING: If the cache is empty, force a load.
        if (!_cache.Any())
        {
            logger.LogWarning("[BusDirectory] ⚠️ 內存快取為空，正在即時修復...");
            await InitializeAsync();
        }

        // 1. Full Cache building for frontend
        if (query.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            return _cache.Select(MapToResult).ToList();
        }

        // 2. Default view
        if (string.IsNullOrWhiteSpace(query))
        {
            return _cache.Select(MapToResult).ToList();
        }

        var q = query.Trim().ToUpper();
        
        // 3. Search Logic
        return _cache
            .Where(r => r.RouteNumber.ToUpper().StartsWith(q))
            .OrderBy(r => r.RouteNumber.Length) 
            .ThenBy(r => r.RouteNumber)
            .Take(100) 
            .Select(MapToResult)
            .ToList();
    }

    /// <summary>
    /// Maps Database Entities to Frontend-friendly DTOs.
    /// </summary>
    public BusRouteSearchResult MapToResult(BusRoute r)
    {
        var comp = (r.Company ?? "").Trim();
        var isCtb = comp.Equals("CTB", StringComparison.OrdinalIgnoreCase) || comp.Equals("NWFB", StringComparison.OrdinalIgnoreCase);
        var bound = r.Bound ?? "O";
        var isReturn = bound.Equals("I", StringComparison.OrdinalIgnoreCase) || bound.Equals("inbound", StringComparison.OrdinalIgnoreCase);

        var orig = r.OriginTc ?? "總站";
        var dest = r.DestinationTc ?? "未知";

        if (isCtb && isReturn)
        {
            orig = r.DestinationTc ?? "總站";
            dest = r.OriginTc ?? "未知";
        }

        // Resilient type detection
        string type = comp.StartsWith("GMB", StringComparison.OrdinalIgnoreCase) ? "minibus" : "bus";

        return new BusRouteSearchResult(
            r.RouteNumber ?? "??",
            bound,
            comp,
            dest,
            orig,
            r.ServiceType ?? "1",
            type
        );
    }
}