using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities.Bus;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Models;
using Mud9Bot.Transport.Services;
using Quartz;

namespace Mud9Bot.Jobs;

/// <summary>
/// Syncs bus routes and stops. Daily at 8 AM.
/// Fix: Optimization logic now "touches" stops to prevent accidental deactivation during cleanup.
/// </summary>
[QuartzJob(Name = "Bus Route Data Update", CronInterval = "0 0 8 * * ?", RunOnStartup = true)]
public class BusDataSyncJob(BotDbContext dbContext, IBusApiService busApiService, IHttpClientFactory httpClientFactory, BusDirectory busDirectory, ILogger<BusDataSyncJob> logger) : IJob
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
        // MTR BUS SPECIFIC SYNC (CSV BASED)
        // ==========================================
        await SyncMtrRoutesFromCsvAsync(dbContext, syncTime);
        
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

        // FIXED: Refresh the Singleton memory cache after DB update is complete!
        // 確保 DB 同步完之後，即刻話比 BusDirectory 聽要重新載入 Memory 資料。
        await busDirectory.RefreshAsync();
        
        logger.LogInformation("大佬！所有巴士資料同步完畢！🚌✨");
    }
    
    /// <summary>
    /// Fetches MTR Bus metadata from Official CSV files.
    /// This removes the dependency on live API uptime and guarantees we have names and coordinates!
    /// </summary>
    private async Task SyncMtrRoutesFromCsvAsync(BotDbContext dbContext, DateTime syncTime)
    {
        logger.LogInformation("[BusSync] 🚆 開始同步 MTR 港鐵巴士路線資料 (透過 CSV)...");
        var client = httpClientFactory.CreateClient();
        
        try
        {
            var routesCsv = await client.GetStringAsync("https://opendata.mtr.com.hk/data/mtr_bus_routes.csv");
            var stopsCsv = await client.GetStringAsync("https://opendata.mtr.com.hk/data/mtr_bus_stops.csv");

            // 1. Parse Routes to get Names and Terminals
            var routeInfoMap = new Dictionary<string, (string OrigTc, string DestTc, string OrigEn, string DestEn)>();
            var routeLines = routesCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 1; i < routeLines.Length; i++) // Skip header
            {
                var cols = ParseCsvLine(routeLines[i]);
                if (cols.Count < 7) continue;

                var routeId = cols[0].Trim();
                var nameTc = cols[1].Trim(); // e.g., "屯門碼頭至兆麟"
                var nameEn = cols[2].Trim(); // e.g., "Tuen Mun Ferry Pier to Siu Lun"
                var refId = cols[6].Trim();  // e.g., "506" or "506-1"

                // Split string by "至" and " to " to determine Origin and Destination
                var tcParts = nameTc.Split("至", 2);
                var enParts = nameEn.Split(" to ", 2);

                string origTc = tcParts.Length > 0 ? tcParts[0] : nameTc;
                string destTc = tcParts.Length > 1 ? tcParts[1] : nameTc;
                
                string origEn = enParts.Length > 0 ? enParts[0] : nameEn;
                string destEn = enParts.Length > 1 ? enParts[1] : nameEn;

                // Clean up any "(循環線)" tags for cleaner UI
                destTc = destTc.Replace(" (循環線)", "").Trim();
                destEn = destEn.Replace(" (Circular)", "").Trim();

                routeInfoMap[refId] = (origTc, destTc, origEn, destEn);
            }

            // 2. Parse Stops and Link to Routes
            var stopLines = stopsCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var stopsData = new List<MtrCsvStopRecord>();
            
            for (int i = 1; i < stopLines.Length; i++)
            {
                var cols = ParseCsvLine(stopLines[i]);
                if (cols.Count < 9) continue;
                
                stopsData.Add(new MtrCsvStopRecord
                {
                    RouteId = cols[0].Trim(),
                    Direction = cols[1].Trim().ToUpper(), // Will be 'O' or 'I'
                    Seq = int.TryParse(cols[2], out int sq) ? sq : 0,
                    StopId = cols[3].Trim(),
                    Lat = double.TryParse(cols[4], out double lat) ? lat : (double?)null,
                    Lon = double.TryParse(cols[5], out double lon) ? lon : (double?)null,
                    NameTc = cols[6].Trim(),
                    NameEn = cols[7].Trim(),
                    RefId = cols[8].Trim()
                });
            }

            // Group strictly by Reference ID and Direction
            var groupedStops = stopsData.GroupBy(s => new { s.RefId, s.Direction });

            foreach (var group in groupedStops)
            {
                string refId = group.Key.RefId;
                string bound = group.Key.Direction == "D" || group.Key.Direction == "I" ? "I" : "O"; 
                var firstStop = group.First();
                string routeNumber = firstStop.RouteId;
                
                // Determine Service Type dynamically
                string serviceType = "1";
                if (refId != routeNumber && refId.Contains('-'))
                {
                    var parts = refId.Split('-');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int varNum))
                    {
                        serviceType = (varNum + 1).ToString();
                    }
                    else serviceType = "2";
                }

                string dbRouteId = $"MTR_{routeNumber}_{bound}_{serviceType}";

                // Resolve Terminals
                string origTc = $"{routeNumber} 總站", destTc = "終點站";
                string origEn = "", destEn = "";
                
                if (routeInfoMap.TryGetValue(refId, out var names))
                {
                    // If it's the inbound (return) trip, swap the names correctly before saving to DB
                    if (bound == "I")
                    {
                        origTc = names.DestTc; destTc = names.OrigTc;
                        origEn = names.DestEn; destEn = names.OrigEn;
                    }
                    else
                    {
                        origTc = names.OrigTc; destTc = names.DestTc;
                        origEn = names.OrigEn; destEn = names.DestEn;
                    }
                }

                // 3. Upsert Route to DB (FIXED: Using FindAsync to prevent Tracking crashes)
                var dbRoute = await dbContext.Set<BusRoute>().FindAsync(dbRouteId);
                if (dbRoute == null)
                {
                    dbRoute = new BusRoute { Id = dbRouteId, Company = "MTR", RouteNumber = routeNumber, Bound = bound, ServiceType = serviceType };
                    dbContext.Add(dbRoute);
                }
                dbRoute.OriginTc = origTc;
                dbRoute.DestinationTc = destTc;
                dbRoute.OriginEn = origEn;
                dbRoute.DestinationEn = destEn;
                dbRoute.IsActive = true;
                dbRoute.LastUpdated = syncTime;

                // 4. Upsert Stops to DB
                foreach (var stop in group.OrderBy(s => s.Seq))
                {
                    if (string.IsNullOrEmpty(stop.StopId)) continue;
                    
                    var dbStop = await dbContext.Set<BusStop>().FindAsync(stop.StopId);
                    if (dbStop == null)
                    {
                        dbStop = new BusStop { StopId = stop.StopId };
                        dbContext.Add(dbStop);
                    }
                    dbStop.NameTc = stop.NameTc;
                    dbStop.NameEn = stop.NameEn;
                    dbStop.Latitude = stop.Lat;
                    dbStop.Longitude = stop.Lon;
                    dbStop.IsActive = true;
                    dbStop.LastUpdated = syncTime;
                    
                    _processedStopIds.Add(stop.StopId);

                    string rsId = $"{dbRouteId}_{stop.Seq}";
                    // FIXED: Using FindAsync to prevent Tracking crashes
                    var dbRS = await dbContext.Set<BusRouteStop>().FindAsync(rsId);
                    if (dbRS == null)
                    {
                        dbRS = new BusRouteStop { Id = rsId, RouteId = dbRouteId, StopId = stop.StopId };
                        dbContext.Add(dbRS);
                    }
                    dbRS.Sequence = stop.Seq;
                    dbRS.IsActive = true;
                    dbRS.LastUpdated = syncTime;
                }
            }

            await dbContext.SaveChangesAsync();
            logger.LogInformation("[BusSync] ✅ MTR 港鐵巴士同步完成 (共處理 {Count} 個路綫方向)。", groupedStops.Count());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BusSync] [MTR] ❌ 讀取或解析 CSV 時發生錯誤");
        }
    }

    // Custom CSV parser to handle fields that contain commas wrapped in quotes
    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                // Ignore trailing carriage returns on Linux/Windows boundary
                if (c != '\r') current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private class MtrCsvStopRecord
    {
        public string RouteId { get; set; } = "";
        public string Direction { get; set; } = "";
        public int Seq { get; set; }
        public string StopId { get; set; } = "";
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public string NameTc { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string RefId { get; set; } = "";
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