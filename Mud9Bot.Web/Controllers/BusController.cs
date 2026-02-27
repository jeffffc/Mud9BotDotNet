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
    /// <summary>
    /// Updated Metadata endpoint with Self-Healing.
    /// Ensures we don't send "0 routes" to the frontend if the cache is still warming up.
    /// </summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata()
    {
        // Trigger a load if a request comes in before the startup task finishes
        if (busDirectory.GetCacheCount() == 0)
        {
            await busDirectory.InitializeAsync();
        }

        return Ok(new {
            lastUpdated = busDirectory.GetLastUpdated().Ticks,
            routeCount = busDirectory.GetCacheCount()
        });
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        var results = await busDirectory.SearchRoutesAsync(q ?? "");
        return Ok(results);
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng)
    {
        double offset = 0.01; 
        var nearbyStops = await dbContext.Set<BusStop>()
            .Where(s => s.IsActive && s.Latitude >= lat - offset && s.Latitude <= lat + offset && s.Longitude >= lng - offset && s.Longitude <= lng + offset)
            .Select(s => s.StopId).ToListAsync();

        if (!nearbyStops.Any()) return Ok(new List<BusRouteSearchResult>());

        var rawRoutes = await dbContext.Set<BusRouteStop>()
            .Where(rs => rs.IsActive && nearbyStops.Contains(rs.StopId))
            .Select(rs => rs.BusRoute).Where(r => r.IsActive).Distinct()
            .OrderBy(r => r.RouteNumber).Take(50).ToListAsync();

        return Ok(rawRoutes.Select(busDirectory.MapToResult).ToList());
    }

    [HttpGet("route/{routeId}/stops")]
    public async Task<IActionResult> GetRouteStops(string routeId)
    {
        var stops = await dbContext.Set<BusRouteStop>()
            .Where(rs => rs.RouteId == routeId && rs.IsActive)
            .Include(rs => rs.BusStop).OrderBy(rs => rs.Sequence) 
            .Select(rs => new { rs.Sequence, rs.StopId, rs.BusStop.NameTc, rs.BusStop.NameEn, rs.BusStop.Latitude, rs.BusStop.Longitude })
            .ToListAsync();
        return Ok(stops);
    }

    [HttpGet("eta/{company}/{stopId}/{route}/{serviceType}")]
    public async Task<IActionResult> GetEta(string company, string stopId, string route, string serviceType, [FromQuery] string? bound)
    {
        try {
            var etas = await busApiService.GetEtasAsync(company, stopId, route, serviceType);
            if (etas == null || !etas.Any()) return Ok(new List<object>());
            if (!string.IsNullOrEmpty(bound)) {
                var targetBound = bound.ToLower();
                string[] inboundMatch = ["i", "inbound"]; string[] outboundMatch = ["o", "outbound"];
                bool isIn = inboundMatch.Contains(targetBound); bool isOut = outboundMatch.Contains(targetBound);
                etas = etas.Where(e => {
                    if (string.IsNullOrEmpty(e.Direction)) return true;
                    var eDir = e.Direction.ToLower();
                    if (isIn) return inboundMatch.Contains(eDir);
                    if (isOut) return outboundMatch.Contains(eDir);
                    return eDir == targetBound;
                }).ToList();
            }
            return Ok(etas.OrderBy(e => e.EtaTime ?? DateTime.MaxValue).ToList());
        } catch (Exception ex) {
            logger.LogError(ex, "[BusAPI] ❌ ETA Fetching crashed for {Route}", route);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}