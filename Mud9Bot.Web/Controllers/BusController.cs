using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Services;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities.Bus;

namespace Mud9Bot.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusController(
    BotDbContext dbContext, 
    IBusApiService busApiService, 
    BusDirectory busDirectory,
    ILogger<BusController> logger) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        logger.LogInformation("[BusAPI] 🔎 Search requested with query: '{Query}'", q ?? "");
        var results = await busDirectory.SearchRoutesAsync(q ?? "");
        logger.LogInformation("[BusAPI] ✅ Search returned {Count} results.", results.Count);
        return Ok(results);
    }

    /// <summary>
    /// 獲取附近路線：根據座標找出站點，並列出經過這些站點的路線。
    /// </summary>
    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng)
    {
        // Increased offset to 0.01 (~1km) for better discovery range
        
        double offset = 0.01; 
        double minLat = lat - offset;
        double maxLat = lat + offset;
        double minLng = lng - offset;
        double maxLng = lng + offset;

        logger.LogInformation("[Nearby] 📍 Received coordinates: Lat={Lat}, Lng={Lng}", lat, lng);
        
        var totalActiveStops = await dbContext.Set<BusStop>().CountAsync(s => s.IsActive);
        logger.LogInformation("[Nearby] 📊 Database Integrity Check: Total active stops in DB = {Total}", totalActiveStops);

        var nearbyStops = await dbContext.Set<BusStop>()
            .Where(s => s.IsActive && 
                        s.Latitude >= minLat && s.Latitude <= maxLat &&
                        s.Longitude >= minLng && s.Longitude <= maxLng)
            .Select(s => s.StopId)
            .ToListAsync();

        logger.LogInformation("[Nearby] 🛑 Found {StopCount} stops within bounding box.", nearbyStops.Count);

        if (!nearbyStops.Any()) 
        {
            return Ok(new List<BusRouteSearchResult>());
        }

        // Fetch entities first so we can apply C# swapping logic for CTB
        var rawRoutes = await dbContext.Set<BusRouteStop>()
            .Where(rs => rs.IsActive && nearbyStops.Contains(rs.StopId))
            .Select(rs => rs.BusRoute)
            .Where(r => r.IsActive)
            .Distinct()
            .OrderBy(r => r.RouteNumber)
            .Take(50)
            .ToListAsync();

        // Map and fix data anomalies (like CTB single-record direction) before sending to UI
        var routes = rawRoutes.Select(r => 
        {
            var isCtb = r.Company == "CTB" || r.Company == "NWFB";
            var isReturn = r.Bound.Equals("I", StringComparison.OrdinalIgnoreCase) || r.Bound.Equals("inbound", StringComparison.OrdinalIgnoreCase);

            var orig = r.OriginTc;
            var dest = r.DestinationTc;

            // Swap names for Citybus inbound routes
            if (isCtb && isReturn)
            {
                orig = r.DestinationTc;
                dest = r.OriginTc;
            }

            return new BusRouteSearchResult(
                r.RouteNumber, r.Bound, r.Company, dest, orig, r.ServiceType);
        }).ToList();

        logger.LogInformation("[Nearby] 🚌 Found {RouteCount} unique routes.", routes.Count);
        return Ok(routes);
    }

    [HttpGet("route/{routeId}/stops")]
    public async Task<IActionResult> GetRouteStops(string routeId)
    {
        logger.LogInformation("[BusAPI] 🛤️ Fetching stops for RouteId: {RouteId}", routeId);
        
        // Use explicit ordering to ensure stops are sequential.
        // 確保車站係跟 Sequence 排，費事去程變咗回程方向。
        
        // Debug logic for reversed bounds
        // 如果發現特定路線（如 272A）嘅 Bound 有問題，可以喺度針對性處理。
        
        // STRICT FIX: Apply OrderBy native to EF Core SQL to guarantee 1, 2, 3 sequence
        var stops = await dbContext.Set<BusRouteStop>()
            .Where(rs => rs.RouteId == routeId && rs.IsActive)
            .Include(rs => rs.BusStop)
            .OrderBy(rs => rs.Sequence) // Native SQL numerical sort
            .Select(rs => new {
                rs.Sequence,
                rs.StopId,
                rs.BusStop.NameTc,
                rs.BusStop.NameEn,
                rs.BusStop.Latitude,
                rs.BusStop.Longitude
            })
            .ToListAsync();

        if (stops.Any())
        {
            logger.LogInformation("[BusAPI] ✅ Returned {Count} stops. Seq range: {Min} to {Max}", 
                stops.Count, stops.First().Sequence, stops.Last().Sequence);
        }
        
        return Ok(stops);
    }

    [HttpGet("eta/{company}/{stopId}/{route}/{serviceType}")]
    public async Task<IActionResult> GetEta(
        string company,
        string stopId,
        string route,
        string serviceType,
        [FromQuery] string? bound)
    {
        try
        {
            var etas = await busApiService.GetEtasAsync(company, stopId, route, serviceType);
            if (etas == null || !etas.Any()) return Ok(new List<object>());

            if (!string.IsNullOrEmpty(bound))
            {
                var targetBound = bound.ToLower();
                
                string[] inboundMatch = ["i", "inbound"];
                string[] outboundMatch = ["o", "outbound"];

                bool isLookingForInbound = inboundMatch.Contains(targetBound);
                bool isLookingForOutbound = outboundMatch.Contains(targetBound);

                etas = etas.Where(e =>
                {
                    if (string.IsNullOrEmpty(e.Direction)) return true;
                    var eDir = e.Direction.ToLower();
                    if (isLookingForInbound) return inboundMatch.Contains(eDir);
                    if (isLookingForOutbound) return outboundMatch.Contains(eDir);
                    return eDir == targetBound;
                }).ToList();
            }

            // GUARANTEE: Sort chronologically. KMB returns 'seq' as the stop sequence, 
            // which causes ties/randomness if used for ETAs. This ensures closest bus is first.
            etas = etas.OrderBy(e => e.EtaTime ?? DateTime.MaxValue).ToList();

            return Ok(etas);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BusAPI] ❌ ETA Fetching crashed for {Route}", route);
            return StatusCode(500, new { error = "Internal server error during ETA fetch" });
        }
    }
}