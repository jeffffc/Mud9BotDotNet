using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities.Bus;
using Mud9Bot.Transport.Models;
using Mud9Bot.Transport.Services;
using Quartz;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "Minibus Route Data Update", CronInterval = "0 30 5 * * ?", RunOnStartup = false)]
public class MinibusDataSyncJob(
    BotDbContext dbContext, 
    IHttpClientFactory httpClientFactory, 
    BusDirectory busDirectory,
    ILogger<MinibusDataSyncJob> logger) : IJob
{
    private const string BaseUrl = "https://data.etagmb.gov.hk";
    
    // Performance Optimization: Memory Maps
    private readonly HashSet<string> _processedStopIds = new();
    private Dictionary<string, DateTime> _existingStopsMap = new();
    private Dictionary<string, BusRoute> _existingRoutesMap = new();
    private HashSet<string> _routesWithStops = new();
    private int _processedVariantCount = 0;

    public async Task Execute(IJobExecutionContext context)
    {
        var syncTime = DateTime.UtcNow;
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        _processedStopIds.Clear();
        _processedVariantCount = 0;

        logger.LogInformation("[MinibusSync] 🟢 開始同步綠色小巴資料...");

        try
        {
            // 0. Pre-load data into memory to prevent EF Core Tracking Bloat & N+1 Queries
            logger.LogInformation("[MinibusSync] 🔍 正在預取站點及路線地圖 (Pre-loading Cache)...");
            
            _existingStopsMap = await dbContext.Set<BusStop>()
                .Select(s => new { s.StopId, s.LastUpdated })
                .ToDictionaryAsync(x => x.StopId, x => x.LastUpdated);

            // Fetch any existing route that starts with GMB (e.g. GMB, GMB_HKI, GMB_KLN)
            _existingRoutesMap = await dbContext.Set<BusRoute>()
                .Where(r => r.Company.StartsWith("GMB"))
                .ToDictionaryAsync(r => r.Id);

            _routesWithStops = await dbContext.Set<BusRouteStop>()
                .Where(rs => rs.RouteId.StartsWith("GMB"))
                .Select(rs => rs.RouteId)
                .Distinct()
                .ToHashSetAsync();

            // 1. Define the 3 required regions according to the GMB API specs
            var regions = new[] { "HKI", "KLN", "NT" };

            foreach (var region in regions)
            {
                logger.LogInformation("[MinibusSync] 📍 正在處理區域: {Region}...", region);
                
                try 
                {
                    // 2. Fetch Route List for the specific region
                    string routeListJson = await client.GetStringAsync($"{BaseUrl}/route/{region}");
                    using var doc = JsonDocument.Parse(routeListJson);
                    
                    var routeCodes = doc.RootElement
                        .GetProperty("data")
                        .GetProperty("routes")
                        .EnumerateArray()
                        .Select(e => e.GetString()!)
                        .ToList();

                    foreach (var routeCode in routeCodes)
                    {
                        try 
                        {
                            // 3. Fetch variants for this route number
                            string encodedRoute = Uri.EscapeDataString(routeCode);
                            var variantsRes = await client.GetFromJsonAsync<GmbRouteDetailResponse>($"{BaseUrl}/route/{region}/{encodedRoute}");
                            if (variantsRes?.Data == null) continue;

                            foreach (var variant in variantsRes.Data)
                            {
                                foreach (var dir in variant.Directions)
                                {
                                    // Pass the region into the processor to use as the Company tag
                                    await ProcessMinibusRoute(client, region, variant, dir, syncTime);
                                    
                                    // BATCH SAVING: Prevent memory bloat and keep FindAsync fast
                                    _processedVariantCount++;
                                    if (_processedVariantCount % 50 == 0)
                                    {
                                        await dbContext.SaveChangesAsync();
                                        logger.LogInformation("[MinibusSync] 目前進度: 已處理 {Count} 個小巴變體...", _processedVariantCount);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning("[MinibusSync] ⚠️ 跳過路線 {RouteCode} 因為讀取錯誤: {Msg}", routeCode, ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("[MinibusSync] ❌ 無法讀取區域 {Region} 嘅路線名單: {Msg}", region, ex.Message);
                }
                
                await dbContext.SaveChangesAsync(); // Save remaining items for this region
            }

            logger.LogInformation("[MinibusSync] 🧹 正在清理舊資料...");

            // Final protection: Ensure any stop that belongs to an active route survives cleanup
            if (_processedStopIds.Any())
            {
                var idList = _processedStopIds.ToList();
                // Chunk into 2000s to avoid Postgres parameter limits
                for (int i = 0; i < idList.Count; i += 2000)
                {
                    var chunk = idList.Skip(i).Take(2000).ToList();
                    await dbContext.Set<BusStop>()
                        .Where(s => chunk.Contains(s.StopId))
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastUpdated, syncTime).SetProperty(p => p.IsActive, true));
                }
            }

            var cleanupThreshold = syncTime.AddSeconds(-1);
            
            // Clean up any old GMB records
            await dbContext.Set<BusRoute>().Where(r => r.Company.StartsWith("GMB") && r.IsActive && r.LastUpdated < cleanupThreshold).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false));
            await dbContext.Set<BusRouteStop>().Where(r => r.RouteId.StartsWith("GMB") && r.IsActive && r.LastUpdated < cleanupThreshold).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false));
            
            await busDirectory.RefreshAsync();
            logger.LogInformation("[MinibusSync] ✅ 綠色小巴同步完畢！");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MinibusSync] ❌ 同步過程中發生嚴重錯誤");
        }
    }

    /// <summary>
    /// Processes a specific Minibus route variant and direction.
    /// Includes the "Light Update" optimization to skip API calls for recent data.
    /// </summary>
    private async Task ProcessMinibusRoute(HttpClient client, string region, GmbRouteVariantDto variant, GmbDirectionDto dir, DateTime syncTime)
    {
        string bound = dir.RouteSeq == 1 ? "O" : "I";
        string dbRouteId = $"GMB_{variant.RouteCode}_{bound}_{variant.RouteId}";
        string regionalCompany = $"GMB_{region}"; // Yields GMB_HKI, GMB_KLN, or GMB_NT

        _existingRoutesMap.TryGetValue(dbRouteId, out var dbRoute);

        // OPTIMIZATION: Light Update Fast-Lane
        // 如果尋日已經同步過，直接用 SQL Update 更新時間戳，完全跳過後面嘅 API calls！
        if (dbRoute != null && dbRoute.LastUpdated > syncTime.AddHours(-20) && _routesWithStops.Contains(dbRouteId))
        {
            // Ensure company is updated from legacy "GMB" to regional format
            dbRoute.Company = regionalCompany;
            dbRoute.LastUpdated = syncTime;
            dbRoute.IsActive = true;

            // Touch junction records
            await dbContext.Set<BusRouteStop>()
                .Where(rs => rs.RouteId == dbRouteId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastUpdated, syncTime).SetProperty(p => p.IsActive, true));
            
            // Mark these stops as processed so they aren't deactivated
            var stopIds = await dbContext.Set<BusRouteStop>()
                .Where(rs => rs.RouteId == dbRouteId)
                .Select(rs => rs.StopId)
                .ToListAsync();
            
            foreach(var sid in stopIds) _processedStopIds.Add(sid);

            return; // Exit early! Saves massive amounts of time.
        }

        logger.LogInformation("[MinibusSync] 🔄 {RouteId} ({Region}) >> 發現新路線或資料已過時，全面同步 (Full Sync)...", dbRouteId, region);

        if (dbRoute == null)
        {
            dbRoute = new BusRoute { 
                Id = dbRouteId, 
                Company = regionalCompany, 
                RouteNumber = variant.RouteCode, 
                Bound = bound, 
                ServiceType = variant.RouteId.ToString() 
            };
            dbContext.Add(dbRoute);
            _existingRoutesMap[dbRouteId] = dbRoute;
        }

        dbRoute.Company = regionalCompany; // Update just in case it existed but missed the Light Update condition
        dbRoute.OriginTc = dir.OriginTc; 
        dbRoute.DestinationTc = dir.DestinationTc;
        dbRoute.OriginEn = dir.OriginEn; 
        dbRoute.DestinationEn = dir.DestinationEn;
        dbRoute.IsActive = true; 
        dbRoute.LastUpdated = syncTime;

        var stopRes = await client.GetFromJsonAsync<GmbRouteStopResponse>($"{BaseUrl}/route-stop/{variant.RouteId}/{dir.RouteSeq}");
        
        if (stopRes?.Data?.RouteStops != null)
        {
            foreach (var stopSeq in stopRes.Data.RouteStops)
            {
                string stopIdStr = stopSeq.StopId.ToString();
                
                if (!_processedStopIds.Contains(stopIdStr))
                {
                    await UpsertMinibusStop(client, stopSeq, syncTime);
                }

                string rsId = $"{dbRouteId}_{stopSeq.StopSeq}";
                var dbRS = await dbContext.Set<BusRouteStop>().FindAsync(rsId);
                if (dbRS == null)
                {
                    dbRS = new BusRouteStop { Id = rsId, RouteId = dbRouteId, StopId = stopIdStr };
                    dbContext.Add(dbRS);
                }
                dbRS.Sequence = stopSeq.StopSeq;
                dbRS.IsActive = true;
                dbRS.LastUpdated = syncTime;
            }
        }
    }

    /// <summary>
    /// Upserts physical stop metadata into the database.
    /// Checks memory map first to avoid unnecessary API calls.
    /// </summary>
    private async Task UpsertMinibusStop(HttpClient client, GmbStopSeqDto stopSeq, DateTime syncTime)
    {
        string sid = stopSeq.StopId.ToString();
        
        // OPTIMIZATION: Check memory map to see if we already have fresh coordinates
        _existingStopsMap.TryGetValue(sid, out var lastUpdated);

        var dbStop = await dbContext.Set<BusStop>().FindAsync(sid);
        
        if (dbStop == null)
        {
            dbStop = new BusStop 
            { 
                StopId = sid,
                NameTc = string.IsNullOrWhiteSpace(stopSeq.NameTc) ? "未知站點" : stopSeq.NameTc,
                NameEn = string.IsNullOrWhiteSpace(stopSeq.NameEn) ? "Unknown Stop" : stopSeq.NameEn,
                IsActive = true,
                LastUpdated = syncTime
            };
            dbContext.Add(dbStop);
        }
        else
        {
            dbStop.IsActive = true;
            dbStop.LastUpdated = syncTime;
        }
        
        // Only fetch the Stop Detail API if we are missing coordinates OR data is older than 7 days
        if (dbStop.Latitude == null || lastUpdated == default || lastUpdated < syncTime.AddDays(-7))
        {
            try {
                var detailRes = await client.GetFromJsonAsync<GmbStopDetailResponse>($"{BaseUrl}/stop/{stopSeq.StopId}");
                if (detailRes?.Data != null)
                {
                    if (!string.IsNullOrWhiteSpace(detailRes.Data.NameTc)) dbStop.NameTc = detailRes.Data.NameTc;
                    if (!string.IsNullOrWhiteSpace(detailRes.Data.NameEn)) dbStop.NameEn = detailRes.Data.NameEn;

                    if (detailRes.Data.Coordinates?.Wgs84 != null)
                    {
                        dbStop.Latitude = detailRes.Data.Coordinates.Wgs84.Latitude;
                        dbStop.Longitude = detailRes.Data.Coordinates.Wgs84.Longitude;
                    }
                    
                    dbStop.LastUpdated = syncTime;
                }
            } catch { /* Fail gracefully, we still have the fallback names */ }
        }
        
        _processedStopIds.Add(sid);
    }
}