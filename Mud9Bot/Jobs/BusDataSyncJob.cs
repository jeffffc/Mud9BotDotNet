using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities.Bus;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Models;
using Quartz;

namespace Mud9Bot.Jobs;

/// <summary>
/// Syncs bus routes and stops. Daily at 8 AM.
/// Fix: Optimization logic now "touches" stops to prevent accidental deactivation during cleanup.
/// </summary>
[QuartzJob(Name = "Bus Route Data Update", CronInterval = "0 0 8 * * ?", RunOnStartup = true)]
public class BusDataSyncJob(BotDbContext dbContext, IBusApiService busApiService, IHttpClientFactory httpClientFactory, ILogger<BusDataSyncJob> logger) : IJob
{
    private readonly HashSet<string> _processedStopIds = new();
    private Dictionary<string, DateTime> _existingStopsMap = new();
    
    // Static list of known MTR Bus routes since they don't provide a /route discovery endpoint
    private static readonly string[] MtrRoutes = [
        "506", "K52", "K52A", "K53", "K54", "K58", 
        "K65", "K65A", "K66", "K68", "K73", "K74", 
        "K75A", "K75P", "K75S", "K76", "K76S"
    ];

    public async Task Execute(IJobExecutionContext context)
    {
        var syncTime = DateTime.UtcNow;
        var apiProviders = new[] { "KMB", "CTB" };
        _processedStopIds.Clear();

        logger.LogInformation("[BusSync] 🔍 正在預取站點地圖 (Stop Map)...");
        _existingStopsMap = await dbContext.Set<BusStop>()
            .Select(s => new { s.StopId, s.LastUpdated })
            .ToDictionaryAsync(x => x.StopId, x => x.LastUpdated);

        // ==========================================
        // MTR BUS SPECIFIC SYNC
        // ==========================================
        await SyncMtrRoutesAsync(dbContext, syncTime);
        
        
        // ==========================================
        // KMB/CTB BUS SPECIFIC SYNC
        // ==========================================
        
        foreach (var provider in apiProviders)
        {
            logger.LogInformation("[BusSync] 🚀 開始同步 {Provider} 來源之路線資料...", provider);
            
            var companiesToFetch = provider switch
            {
                "KMB" => new[] { "KMB", "LWB" },
                "CTB" or "NWFB" => new[] { "CTB", "NWFB" },
                _ => new[] { provider }
            };
            
            var existingRoutes = await dbContext.Set<BusRoute>()
                .Where(r => companiesToFetch.Contains(r.Company))
                .ToDictionaryAsync(r => r.Id);

            var routesWithStops = await dbContext.Set<BusRouteStop>()
                .Where(rs => rs.RouteId.StartsWith("KMB") || rs.RouteId.StartsWith("LWB") || rs.RouteId.StartsWith("CTB") || rs.RouteId.StartsWith("NWFB"))
                .Select(rs => rs.RouteId)
                .Distinct()
                .ToHashSetAsync();

            var apiRoutes = await busApiService.GetRoutesAsync(provider);
            int routeCount = 0;
            int totalRoutes = apiRoutes.Count;

            foreach (var apiRoute in apiRoutes)
            {
                string actualCompany = DetermineActualCompany(apiRoute, provider);
                var boundsToProcess = GetBounds(apiRoute, provider);

                foreach (var effectiveBound in boundsToProcess)
                {
                    if (string.IsNullOrEmpty(apiRoute.Route)) continue;

                    string serviceType = apiRoute.ServiceType ?? "1";
                    var routeId = $"{actualCompany}_{apiRoute.Route}_{effectiveBound}_{serviceType}";
                    
                    existingRoutes.TryGetValue(routeId, out var dbRoute);
                    
                    // FIXED: Resume Logic now updates timestamps for child stops
                    // 修正：如果跳過路線，都要更新返關聯站點嘅時間，廢事俾尾段個 Cleanup 殺死。
                    if (dbRoute != null && dbRoute.LastUpdated > syncTime.AddHours(-20) && routesWithStops.Contains(routeId))
                    {
                        logger.LogInformation("[BusSync] [{Provider}] ⚡ {RouteId} >> 資料仲好新，執行 Light Update (更新時間戳)。", provider, routeId);
                        
                        dbRoute.LastUpdated = syncTime;
                        dbRoute.IsActive = true;

                        // Touch junction records
                        await dbContext.Set<BusRouteStop>()
                            .Where(rs => rs.RouteId == routeId)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastUpdated, syncTime).SetProperty(p => p.IsActive, true));
                        
                        // Mark these stops as processed so they aren't deactivated
                        var stopIds = await dbContext.Set<BusRouteStop>()
                            .Where(rs => rs.RouteId == routeId)
                            .Select(rs => rs.StopId)
                            .ToListAsync();
                        
                        foreach(var sid in stopIds) _processedStopIds.Add(sid);

                        continue;
                    }

                    logger.LogInformation("[BusSync] [{Provider}] 🔄 {RouteId} >> 發現新路線或資料已過時，開始全面同步 (Full Sync)...", provider, routeId);

                    if (dbRoute == null)
                    {
                        dbRoute = new BusRoute { 
                            Id = routeId, 
                            Company = actualCompany, 
                            RouteNumber = apiRoute.Route, 
                            Bound = effectiveBound, 
                            ServiceType = serviceType
                        };
                        dbContext.Add(dbRoute);
                        existingRoutes[routeId] = dbRoute;
                    }

                    dbRoute.OriginTc = apiRoute.OriginTc ?? "";
                    dbRoute.OriginEn = apiRoute.OriginEn ?? "";
                    dbRoute.DestinationTc = apiRoute.DestinationTc ?? "";
                    dbRoute.DestinationEn = apiRoute.DestinationEn ?? "";
                    dbRoute.IsActive = true;
                    dbRoute.LastUpdated = syncTime;

                    try 
                    {
                        var apiStops = await busApiService.GetRouteStopsAsync(actualCompany, apiRoute.Route, effectiveBound, serviceType);
                        foreach (var apiStop in apiStops)
                        {
                            bool stopValid = await UpsertStopDetails(actualCompany, apiStop.StopId, syncTime);
                            if (stopValid)
                            {
                                var rsId = $"{routeId}_{apiStop.Sequence}";
                                var dbRS = await dbContext.Set<BusRouteStop>().FirstOrDefaultAsync(x => x.Id == rsId);
                                if (dbRS == null)
                                {
                                    dbRS = new BusRouteStop { Id = rsId, RouteId = routeId, StopId = apiStop.StopId, Sequence = apiStop.Sequence };
                                    dbContext.Add(dbRS);
                                }
                                dbRS.IsActive = true;
                                dbRS.LastUpdated = syncTime;
                            }
                        }
                    }
                    catch (Exception ex) { logger.LogError(ex, "[!] {Route} 同步失敗", routeId); }
                }

                routeCount++;
                if (routeCount % 20 == 0) 
                {
                    logger.LogInformation("[BusSync] [{Provider}] 目前進度: {Count}/{Total} 條路線已處理...", provider, routeCount, totalRoutes);
                    await dbContext.SaveChangesAsync();
                }
            }
            await dbContext.SaveChangesAsync();
            logger.LogInformation("[BusSync] ✅ {Provider} 同步完成。", provider);
        }

        logger.LogInformation("[BusSync] 🧹 正在清理舊資料...");
        
        // Final protection: Ensure any stop that belongs to an active route survives
        if (_processedStopIds.Any())
        {
            await dbContext.Set<BusStop>()
                .Where(s => _processedStopIds.Contains(s.StopId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastUpdated, syncTime).SetProperty(p => p.IsActive, true));
        }

        await dbContext.Set<BusRoute>().Where(r => r.IsActive && r.LastUpdated < syncTime).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false));
        await dbContext.Set<BusStop>().Where(r => r.IsActive && r.LastUpdated < syncTime).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false));
        await dbContext.Set<BusRouteStop>().Where(r => r.IsActive && r.LastUpdated < syncTime).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false));

        logger.LogInformation("大佬！所有巴士資料同步完畢！🚌✨");
    }
    
    /// <summary>
    /// Processes MTR routes by extracting static topology from the monolithic ETA endpoint.
    /// </summary>
    private async Task SyncMtrRoutesAsync(BotDbContext dbContext, DateTime syncTime)
    {
        logger.LogInformation("[BusSync] 🚆 開始同步 MTR 港鐵巴士路線資料...");
        var client = httpClientFactory.CreateClient();
        const string MtrBusApiUrl = "https://rt.data.gov.hk/v1/transport/mtr/bus/getSchedule";

        foreach (var route in MtrRoutes)
        {
            try
            {
                var requestBody = new { language = "zh", routeName = route };
                var response = await client.PostAsJsonAsync(MtrBusApiUrl, requestBody);
                if (!response.IsSuccessStatusCode) continue;

                var mtrData = await response.Content.ReadFromJsonAsync<MtrBusResponse>();
                
                // 加上防呆機制：如果 Data Model 解析出嚟係 null，馬上提醒！
                if (mtrData?.RouteStops == null || !mtrData.RouteStops.Any()) 
                {
                    logger.LogWarning("[BusSync] [MTR] ⚠️ {Route} 解析唔到站點資料 (可能係 BusApiModels 嘅 JSON 名唔啱，搵唔到 'busStop' 欄位)。", route);
                    continue;
                }

                // MTR groups bounds inside the busStopId (e.g., "K52-U-1" for Up/Outbound, "K52-D-1" for Down/Inbound)
                var upStops = mtrData.RouteStops.Where(s => s.BusStopId.Contains("-U-")).ToList();
                var downStops = mtrData.RouteStops.Where(s => s.BusStopId.Contains("-D-")).ToList();

                await ProcessMtrBound(dbContext, syncTime, route, "O", upStops);
                await ProcessMtrBound(dbContext, syncTime, route, "I", downStops);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[BusSync] [MTR] ❌ {Route} 同步失敗", route);
            }
        }
        await dbContext.SaveChangesAsync();
        logger.LogInformation("[BusSync] ✅ MTR 港鐵巴士同步完成。");
    }

    private async Task ProcessMtrBound(BotDbContext dbContext, DateTime syncTime, string route, string bound, List<MtrBusRouteStop> stops)
    {
        if (!stops.Any()) return;

        string routeId = $"MTR_{route}_{bound}_1";
        
        var dbRoute = await dbContext.Set<BusRoute>().FirstOrDefaultAsync(r => r.Id == routeId);
        if (dbRoute == null)
        {
            dbRoute = new BusRoute { Id = routeId, Company = "MTR", RouteNumber = route, Bound = bound, ServiceType = "1" };
            dbContext.Add(dbRoute);
        }
        
        // MTR doesn't explicitly provide Origin/Dest fields, so we infer them from the first and last stop names
        dbRoute.OriginTc = stops.First().BusStopName;
        dbRoute.DestinationTc = stops.Last().BusStopName;
        dbRoute.OriginEn = dbRoute.OriginTc; 
        dbRoute.DestinationEn = dbRoute.DestinationTc;
        dbRoute.IsActive = true;
        dbRoute.LastUpdated = syncTime;

        int seq = 1;
        foreach (var stop in stops)
        {
            string stopId = stop.BusStopId;
            
            var dbStop = await dbContext.Set<BusStop>().FindAsync(stopId);
            if (dbStop == null)
            {
                dbStop = new BusStop { StopId = stopId };
                dbContext.Add(dbStop);
            }
            dbStop.NameTc = stop.BusStopName;
            dbStop.NameEn = stop.BusStopName;
            dbStop.Latitude = double.TryParse(stop.Latitude, out var lat) ? lat : null;
            dbStop.Longitude = double.TryParse(stop.Longitude, out var lon) ? lon : null;
            dbStop.IsActive = true;
            dbStop.LastUpdated = syncTime;
            
            _processedStopIds.Add(stopId);

            string rsId = $"{routeId}_{seq}";
            var dbRS = await dbContext.Set<BusRouteStop>().FirstOrDefaultAsync(x => x.Id == rsId);
            if (dbRS == null)
            {
                dbRS = new BusRouteStop { Id = rsId, RouteId = routeId, StopId = stopId };
                dbContext.Add(dbRS);
            }
            dbRS.Sequence = seq;
            dbRS.IsActive = true;
            dbRS.LastUpdated = syncTime;
            
            seq++;
        }
    }

    private List<string> GetBounds(Mud9Bot.Transport.Models.BusRouteDto apiRoute, string provider)
    {
        var bounds = new List<string>();
        if (!string.IsNullOrEmpty(apiRoute.Bound)) bounds.Add(apiRoute.Bound);
        else if (!string.IsNullOrEmpty(apiRoute.Dir)) bounds.Add(apiRoute.Dir);
        else if (provider != "KMB") { bounds.Add("inbound"); bounds.Add("outbound"); }
        return bounds;
    }

    private string DetermineActualCompany(Mud9Bot.Transport.Models.BusRouteDto apiRoute, string provider)
    {
        if (!string.IsNullOrEmpty(apiRoute.CompanyId)) return apiRoute.CompanyId.ToUpper();
        if (provider == "KMB")
        {
            var route = apiRoute.Route.ToUpper();
            if (route.StartsWith("A") || route.StartsWith("E") || route.StartsWith("R") || route.StartsWith("S") || route.StartsWith("NA")) return "LWB";
            if (route.StartsWith("X")) return (new[] { "X42C", "X6C", "X89D", "X42P" }).Contains(route) ? "KMB" : "LWB";
            if (route.StartsWith("N")) return (new[] { "N31", "N64", "N42A", "N42" }).Contains(route) ? "LWB" : "KMB";
            return "KMB";
        }
        return provider;
    }

    private async Task<bool> UpsertStopDetails(string company, string stopId, DateTime syncTime)
    {
        if (string.IsNullOrEmpty(stopId)) return false;
        if (_processedStopIds.Contains(stopId)) return true;

        _existingStopsMap.TryGetValue(stopId, out var lastUpdated);
        if (lastUpdated == default || lastUpdated < syncTime.AddHours(-20))
        {
            var details = await busApiService.GetStopDetailsAsync(company, stopId);
            if (details != null && !string.IsNullOrWhiteSpace(details.NameTc))
            {
                var dbStop = await dbContext.Set<BusStop>().FindAsync(stopId);
                if (dbStop == null) { dbStop = new BusStop { StopId = stopId }; dbContext.Add(dbStop); }
                dbStop.NameTc = details.NameTc;
                dbStop.NameEn = string.IsNullOrWhiteSpace(details.NameEn) ? details.NameTc : details.NameEn;
                dbStop.Latitude = double.TryParse(details.Latitude, out var lat) ? lat : null;
                dbStop.Longitude = double.TryParse(details.Longitude, out var lon) ? lon : null;
                dbStop.IsActive = true;
                dbStop.LastUpdated = syncTime;
            }
        }
        _processedStopIds.Add(stopId);
        return true;
    }
}