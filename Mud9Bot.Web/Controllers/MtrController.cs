using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Models;

namespace Mud9Bot.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MtrController(IMtrApiService mtrApiService, ILogger<MtrController> logger) : ControllerBase
{
    /// <summary>
    /// Gets the static list of supported MTR lines.
    /// 攞支援嘅港鐵路綫名單。
    /// </summary>
    [HttpGet("lines")]
    public IActionResult GetLines()
    {
        var lines = mtrApiService.GetLines();
        return Ok(lines);
    }

    /// <summary>
    /// Gets the static sequence of stations for a specific line.
    /// 攞某一條路綫嘅車站名單。
    /// </summary>
    [HttpGet("stations/{line}")]
    public IActionResult GetStations(string line)
    {
        var stations = mtrApiService.GetStationsForLine(line);
        return Ok(stations);
    }

    /// <summary>
    /// Fetches the live ETA for a specific line and station.
    /// 攞即時到站時間，並拆解 MTR 動態 JSON Key 方便前端處理。
    /// </summary>
    [HttpGet("eta/{line}/{station}")]
    public async Task<IActionResult> GetEta(string line, string station)
    {
        try
        {
            var schedule = await mtrApiService.GetScheduleAsync(line, station);
            
            if (schedule?.Data == null) 
                return Ok(new { error = "No data available", message = schedule?.Message });

            // The MTR API nests the result under a dynamic key formatted as "LINE-STATION"
            // 港鐵嘅 JSON Key 係動態嘅 (例如 "TKL-TKO")，我哋喺度幫前端拆開佢
            var key = $"{line.ToUpper()}-{station.ToUpper()}";
            
            if (schedule.Data.TryGetValue(key, out var stationData))
            {
                // Return just the UP and DOWN arrays to the frontend
                return Ok(stationData);
            }
            
            return Ok(new { error = "Station not found in response payload" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MTR API] ❌ ETA Fetching crashed for {Line}-{Station}", line, station);
            return StatusCode(500, new { error = "Internal server error during MTR fetch" });
        }
    }
}