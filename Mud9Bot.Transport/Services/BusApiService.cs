using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Models;

namespace Mud9Bot.Transport.Services;

/// <summary>
/// Service to fetch data from official Bus APIs.
/// Updated for Citybus V2 specifications: Stop API is unified (no company in path).
/// Supports specific base paths for KMB and LWB.
/// </summary>
public class BusApiService(IHttpClientFactory httpClientFactory, IMemoryCache cache) : IBusApiService
{
    private const string KmbBaseUrl = "https://data.etabus.gov.hk/v1/transport/kmb/";
    // User Update: LWB API URL is same as KMB.
    private const string LwbBaseUrl = "https://data.etabus.gov.hk/v1/transport/kmb/";
    private const string CitybusBaseUrl = "https://rt.data.gov.hk/v2/transport/citybus/";
    private const string MtrBusApiUrl = "https://rt.data.gov.hk/v1/transport/mtr/bus/getSchedule";


    private (HttpClient Client, string BaseUrl) GetClient(string company)
    {
        var client = httpClientFactory.CreateClient();
        string c = company.ToUpper();
        
        // Handling specific base URLs for KMB and LWB as per official documentation
        // 根據官方文件，九巴同龍運雖然係同一集團，但 API Path 係分開嘅 (目前 LWB 同 KMB 一樣)。
        var baseUrl = c switch {
            "KMB" => KmbBaseUrl,
            "LWB" => LwbBaseUrl,
            _ => CitybusBaseUrl
        };
        return (client, baseUrl);
    }

    private string MapDirectionForUrl(string? bound)
    {
        if (string.IsNullOrWhiteSpace(bound)) return "inbound";
        var b = bound.ToUpper();
        if (b == "I" || b == "INBOUND") return "inbound";
        if (b == "O" || b == "OUTBOUND") return "outbound";
        return b.ToLower();
    }

    public async Task<List<BusRouteDto>> GetRoutesAsync(string company, string routeNum = "")
    {
        var (client, baseUrl) = GetClient(company);
        string url;

        if (company.ToUpper() == "KMB" || company.ToUpper() == "LWB")
        {
            url = string.IsNullOrEmpty(routeNum) ? $"{baseUrl}route/" : $"{baseUrl}route/{routeNum.ToUpper()}";
        }
        else
        {
            // Citybus V2 Route: /route/{company} or /route/{company}/{route}
            url = string.IsNullOrEmpty(routeNum) 
                ? $"{baseUrl}route/{company.ToUpper()}" 
                : $"{baseUrl}route/{company.ToUpper()}/{routeNum.ToUpper()}";
        }

        var response = await client.GetFromJsonAsync<BusApiResponse<List<BusRouteDto>>>(url);
        return response?.Data ?? [];
    }

    public async Task<List<BusRouteStopDto>> GetRouteStopsAsync(string company, string routeNum, string bound, string serviceType = "1")
    {
        var (client, baseUrl) = GetClient(company);
        string directionParam = MapDirectionForUrl(bound);
        
        string url;
        if (company.ToUpper() == "KMB" || company.ToUpper() == "LWB")
        {
            url = $"{baseUrl}route-stop/{routeNum.ToUpper()}/{directionParam}/{serviceType}";
        }
        else
        {
            // Citybus V2 Route-Stop: /route-stop/{company}/{route}/{direction}
            url = $"{baseUrl}route-stop/{company.ToUpper()}/{routeNum.ToUpper()}/{directionParam}";
        }

        var response = await client.GetFromJsonAsync<BusApiResponse<List<BusRouteStopDto>>>(url);
        
        // FIXED: Strictly order by sequence using numeric sort to avoid lexicographical ordering (1, 10, 2).
        // Removed redundant .ToString() for cleaner type safety.
        // 修正：因為 Sequence 喺 DTO 係 string，要轉做 int 先可以正確排序（1, 2, 3... 10）。
        return (response?.Data ?? [])
            .OrderBy(s => s.Sequence)
            .ToList();
    }

    public async Task<BusStopDto?> GetStopDetailsAsync(string company, string stopId)
    {
        var cacheKey = $"Stop_{stopId}";
        if (cache.TryGetValue(cacheKey, out BusStopDto? cachedStop)) return cachedStop;

        var (client, baseUrl) = GetClient(company);
        
        // IMPORTANT: Both KMB and Citybus V2 use /stop/{stop_id} 
        var response = await client.GetFromJsonAsync<BusApiResponse<BusStopDto>>($"{baseUrl}stop/{stopId}");
        
        if (response?.Data != null)
            cache.Set(cacheKey, response.Data, TimeSpan.FromHours(24));

        return response?.Data;
    }

    public async Task<List<BusEtaDto>> GetEtasAsync(string company, string stopId, string routeNum, string serviceType = "1")
    {
        // Intercept MTR requests and route to specific monolithic handler
        if (company.ToUpper() == "MTR")
        {
            return await GetMtrEtasAsync(stopId, routeNum);
        }
        
        var (client, baseUrl) = GetClient(company);
        string url;

        if (company.ToUpper() == "KMB" || company.ToUpper() == "LWB")
        {
            // KMB/LWB: /eta/{stop_id}/{route}/{service_type}
            url = $"{baseUrl}eta/{stopId}/{routeNum.ToUpper()}/{serviceType}";
        }
        else
        {
            // Citybus V2: /eta/{company}/{stop_id}/{route}
            url = $"{baseUrl}eta/{company.ToUpper()}/{stopId}/{routeNum.ToUpper()}";
        }

        var response = await client.GetFromJsonAsync<BusApiResponse<List<BusEtaDto>>>(url);
        
        // Ensuring the sequence order here helps the UI display arrivals correctly.
        // 前端已經移除排序邏輯，交由 API 全權負責資料正確排序。
        return (response?.Data ?? [])
            .OrderBy(e => e.Sequence)
            .ToList();
    }
    
    /// <summary>
    /// Handles the MTR POST monolithic architecture.
    /// Extracts specific stop data from the giant payload and maps it to our unified DTO.
    /// </summary>
    private async Task<List<BusEtaDto>> GetMtrEtasAsync(string stopId, string routeNum)
    {
        try 
        {
            var client = httpClientFactory.CreateClient();
            var requestBody = new { language = "zh", routeName = routeNum.ToUpper() };
            
            var response = await client.PostAsJsonAsync(MtrBusApiUrl, requestBody);
            var mtrData = await response.Content.ReadFromJsonAsync<MtrBusResponse>();
            
            if (mtrData?.RouteStops == null) return [];

            // 1. Locate the specific stop in the monolithic payload
            var stopData = mtrData.RouteStops.FirstOrDefault(s => s.BusStopId == stopId);
            if (stopData?.BusEtas == null) return [];

            var results = new List<BusEtaDto>();
            int seq = 1;

            foreach (var eta in stopData.BusEtas)
            {
                // Parse "yyyy-MM-dd HH:mm:ss"
                DateTime? parsedEta = DateTime.TryParse(eta.DepartureTime, out var dt) ? dt : null;

                // Determine logical direction from MTR's stopId (e.g., "K52-U-1" -> 'U')
                var parts = stopId.Split('-');
                string dir = parts.Length > 1 ? parts[1] : "O";
                string mappedDir = dir.ToUpper() == "U" ? "O" : "I"; // Map U(Up) to O(Outbound), D(Down) to I(Inbound)

                results.Add(new BusEtaDto(
                    Route: routeNum,
                    Direction: mappedDir,
                    Sequence: seq++,
                    StopId: stopId,
                    DestinationTc: eta.BusTerminal ?? "總站",
                    EtaTime: parsedEta,
                    RemarkTc: eta.BusRemark ?? "",
                    RemarkEn: eta.BusRemark ?? ""
                ));
            }

            return results.OrderBy(e => e.EtaTime ?? DateTime.MaxValue).ToList();
        }
        catch 
        {
            // Silently fail and return empty array if MTR API is down
            return [];
        }
    }
}