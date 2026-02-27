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
/// </summary>
public record BusRouteSearchResult(
    [property: JsonPropertyName("route")] string route, 
    [property: JsonPropertyName("bound")] string bound, 
    [property: JsonPropertyName("company")] string company, 
    [property: JsonPropertyName("dest_tc")] string dest_tc,
    [property: JsonPropertyName("orig_tc")] string orig_tc,
    [property: JsonPropertyName("service_type")] string service_type,
    [property: JsonPropertyName("type")] string type 
);

/// <summary>
/// High-performance Singleton cache for Bus and Minibus routes.
/// </summary>
public class BusDirectory(IServiceScopeFactory scopeFactory, ILogger<BusDirectory> logger)
{
    private List<BusRoute> _cache = new();
    private DateTime _lastUpdated = DateTime.MinValue;

    public int GetCacheCount(string? type = null) 
    {
        if (string.IsNullOrEmpty(type)) return _cache.Count;
        return _cache.Count(r => GetType(r) == type.ToLower());
    }

    public DateTime GetLastUpdated() => _lastUpdated;

    private string GetType(BusRoute r) => (r.Company ?? "").StartsWith("GMB", StringComparison.OrdinalIgnoreCase) ? "minibus" : "bus";

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

            int busCount = _cache.Count(r => GetType(r) == "bus");
            int gmbCount = _cache.Count(r => GetType(r) == "minibus");

            logger.LogInformation("[BusDirectory] ✅ 預載完成！總數: {Total}, 巴士: {Buses}, 小巴: {Minibuses}", 
                _cache.Count, busCount, gmbCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BusDirectory] ❌ 預載路綫失敗！");
        }
    }

    public async Task RefreshAsync() => await InitializeAsync();

    /// <summary>
    /// Searches the memory cache with optional type filtering (bus/minibus).
    /// </summary>
    public async Task<List<BusRouteSearchResult>> SearchRoutesAsync(string query, string? type = null)
    {
        if (!_cache.Any())
        {
            await InitializeAsync();
        }

        // 1. Establish the pool based on type
        IEnumerable<BusRoute> pool = _cache;
        if (!string.IsNullOrEmpty(type))
        {
            var targetType = type.ToLower();
            pool = pool.Where(r => GetType(r) == targetType);
        }

        // 2. Full Cache building for frontend
        if (query.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            return pool.Select(MapToResult).ToList();
        }

        // 3. Search Logic
        var q = query.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(q))
        {
            return pool.Take(100).Select(MapToResult).ToList();
        }
        
        return pool
            .Where(r => r.RouteNumber.ToUpper().StartsWith(q))
            .OrderBy(r => r.RouteNumber.Length) 
            .ThenBy(r => r.RouteNumber)
            .Take(100) 
            .Select(MapToResult)
            .ToList();
    }

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

        return new BusRouteSearchResult(
            r.RouteNumber ?? "??",
            bound,
            comp,
            dest,
            orig,
            r.ServiceType ?? "1",
            GetType(r)
        );
    }
}