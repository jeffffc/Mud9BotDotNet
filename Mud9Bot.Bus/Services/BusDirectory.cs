using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities.Bus;

namespace Mud9Bot.Bus.Services;

public record BusRouteSearchResult(
    string route, 
    string bound, 
    string company, 
    string dest_tc,
    string orig_tc,
    string service_type
);

public class BusDirectory(IServiceScopeFactory scopeFactory, ILogger<BusDirectory> logger)
{
    private static List<BusRoute> _staticRoutes = new();
    private static DateTime _staticLastUpdated = DateTime.MinValue;

    public int GetCacheCount() => _staticRoutes.Count;
    public DateTime GetLastUpdated() => _staticLastUpdated;

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            // 抓取所有 Active 路線並按號碼排序
            _staticRoutes = await dbContext.Set<BusRoute>()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RouteNumber)
                .AsNoTracking()
                .ToListAsync();

            _staticLastUpdated = DateTime.UtcNow;
            logger.LogInformation("[BusDirectory] ✅ 成功預載 {Count} 條路線。", _staticRoutes.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BusDirectory] ❌ 初始化失敗。");
        }
    }

    public Task<List<BusRouteSearchResult>> SearchRoutesAsync(string query)
    {
        // 如果 query 為 ALL，回傳全量數據（用於前端 LocalStorage 緩存）
        if (query.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(_staticRoutes.Select(MapToResult).ToList());
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // 預設首頁：顯示全公司前 30 條
            return Task.FromResult(_staticRoutes.Take(30).Select(MapToResult).ToList());
        }

        var q = query.Trim().ToUpper();
        
        // 嚴格執行 StartsWith 搜尋，並按長度及號碼排序
        var results = _staticRoutes
            .Where(r => r.RouteNumber.ToUpper().StartsWith(q))
            .OrderBy(r => r.RouteNumber.Length) 
            .ThenBy(r => r.RouteNumber)
            .Take(100) // 限制回傳量，其餘交給前端處理
            .Select(MapToResult)
            .ToList();

        return Task.FromResult(results);
    }

    private BusRouteSearchResult MapToResult(BusRoute r)
    {
        var isCtb = r.Company == "CTB" || r.Company == "NWFB";
        var isReturn = r.Bound.Equals("I", StringComparison.OrdinalIgnoreCase) || r.Bound.Equals("inbound", StringComparison.OrdinalIgnoreCase);

        var orig = r.OriginTc;
        var dest = r.DestinationTc;

        // Apply identical UI data normalization for CTB inbound routes
        if (isCtb && isReturn)
        {
            orig = r.DestinationTc;
            dest = r.OriginTc;
        }

        return new BusRouteSearchResult(
            r.RouteNumber,
            r.Bound,
            r.Company,
            dest,
            orig,
            r.ServiceType
        );
    }

    public async Task RefreshAsync() => await InitializeAsync();
}