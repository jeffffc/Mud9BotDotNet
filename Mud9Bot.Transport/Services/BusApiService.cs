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
/// Standardized with User-Agent headers and URL Encoding to prevent 403/404 errors.
/// Supports KMB, LWB, Citybus (V2), MTR, NLB, and GMB.
/// </summary>
public class BusApiService(IHttpClientFactory httpClientFactory, IMemoryCache cache) : IBusApiService
{
    private const string KmbBaseUrl = "https://data.etabus.gov.hk/v1/transport/kmb/";
    private const string LwbBaseUrl = "https://data.etabus.gov.hk/v1/transport/kmb/";
    private const string CitybusBaseUrl = "https://rt.data.gov.hk/v2/transport/citybus/";
    private const string MtrBusApiUrl = "https://rt.data.gov.hk/v1/transport/mtr/bus/getSchedule";
    private const string NlbApiUrl = "https://rt.data.gov.hk/v2/transport/nlb/stop.php";
    private const string GmbBaseUrl = "https://data.etagmb.gov.hk";

    /// <summary>
    /// 核心標準化：所有 HttpClient 都經呢度出，強制加上 User-Agent，防止被政府 API Block (403 Forbidden)。
    /// </summary>
    private HttpClient GetStandardClient()
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    private (HttpClient Client, string BaseUrl) GetClient(string company)
    {
        var client = GetStandardClient();
        string c = company.ToUpper();
        
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

    // ==========================================
    // PUBLIC INTERFACE METHODS
    // ==========================================

    public async Task<List<BusRouteDto>> GetRoutesAsync(string company, string routeNum = "")
    {
        if (company.ToUpper() == "MTR" || company.ToUpper() == "NLB") return [];
        if (company.ToUpper() == "GMB") return await GetGmbRoutesAsync(routeNum);
        
        var (client, baseUrl) = GetClient(company);
        string url;

        // 核心標準化：所有參數必須經過 EscapeDataString 處理 (防 404)
        if (company.ToUpper() == "KMB" || company.ToUpper() == "LWB")
        {
            url = string.IsNullOrEmpty(routeNum) 
                ? $"{baseUrl}route/" 
                : $"{baseUrl}route/{Uri.EscapeDataString(routeNum.ToUpper())}";
        }
        else
        {
            url = string.IsNullOrEmpty(routeNum) 
                ? $"{baseUrl}route/{Uri.EscapeDataString(company.ToUpper())}" 
                : $"{baseUrl}route/{Uri.EscapeDataString(company.ToUpper())}/{Uri.EscapeDataString(routeNum.ToUpper())}";
        }

        var response = await client.GetFromJsonAsync<BusApiResponse<List<BusRouteDto>>>(url);
        return response?.Data ?? [];
    }

    public async Task<List<BusRouteStopDto>> GetRouteStopsAsync(string company, string routeNum, string bound, string serviceType = "1")
    {
        if (company.ToUpper() == "MTR" || company.ToUpper() == "NLB") return [];
        if (company.ToUpper() == "GMB") return await GetGmbRouteStopsAsync(routeNum, bound); // routeNum = id, bound = seq
        
        var (client, baseUrl) = GetClient(company);
        string directionParam = MapDirectionForUrl(bound);
        
        string url;
        if (company.ToUpper() == "KMB" || company.ToUpper() == "LWB")
        {
            url = $"{baseUrl}route-stop/{Uri.EscapeDataString(routeNum.ToUpper())}/{Uri.EscapeDataString(directionParam)}/{Uri.EscapeDataString(serviceType)}";
        }
        else
        {
            url = $"{baseUrl}route-stop/{Uri.EscapeDataString(company.ToUpper())}/{Uri.EscapeDataString(routeNum.ToUpper())}/{Uri.EscapeDataString(directionParam)}";
        }

        var response = await client.GetFromJsonAsync<BusApiResponse<List<BusRouteStopDto>>>(url);
        
        return (response?.Data ?? [])
            .OrderBy(s => s.Sequence)
            .ToList();
    }

    public async Task<BusStopDto?> GetStopDetailsAsync(string company, string stopId)
    {
        if (company.ToUpper() == "MTR" || company.ToUpper() == "NLB") return null;
        if (company.ToUpper() == "GMB") return await GetGmbStopDetailsAsync(stopId);
        
        var cacheKey = $"Stop_{stopId}";
        if (cache.TryGetValue(cacheKey, out BusStopDto? cachedStop)) return cachedStop;

        var (client, baseUrl) = GetClient(company);
        
        var response = await client.GetFromJsonAsync<BusApiResponse<BusStopDto>>($"{baseUrl}stop/{Uri.EscapeDataString(stopId)}");
        
        if (response?.Data != null)
            cache.Set(cacheKey, response.Data, TimeSpan.FromHours(24));

        return response?.Data;
    }

    public async Task<List<BusEtaDto>> GetEtasAsync(string company, string stopId, string routeNum, string serviceType = "1")
    {
        if (company.ToUpper() == "MTR") return await GetMtrEtasAsync(stopId, routeNum);
        if (company.ToUpper() == "NLB") return await GetNlbEtasAsync(stopId, serviceType, routeNum);
        if (company.ToUpper() == "GMB") return await GetGmbEtasAsync(stopId, serviceType, routeNum);
        
        var (client, baseUrl) = GetClient(company);
        string url;

        if (company.ToUpper() == "KMB" || company.ToUpper() == "LWB")
        {
            url = $"{baseUrl}eta/{Uri.EscapeDataString(stopId)}/{Uri.EscapeDataString(routeNum.ToUpper())}/{Uri.EscapeDataString(serviceType)}";
        }
        else
        {
            url = $"{baseUrl}eta/{Uri.EscapeDataString(company.ToUpper())}/{Uri.EscapeDataString(stopId)}/{Uri.EscapeDataString(routeNum.ToUpper())}";
        }

        var response = await client.GetFromJsonAsync<BusApiResponse<List<BusEtaDto>>>(url);
        
        return (response?.Data ?? [])
            .OrderBy(e => e.Sequence)
            .ToList();
    }
    
    // ==========================================
    // MTR / NLB / GMB PRIVATE HANDLERS
    // ==========================================

    private async Task<List<BusEtaDto>> GetMtrEtasAsync(string stopId, string routeNum)
    {
        try 
        {
            var client = GetStandardClient();
            var requestBody = new { language = "zh", routeName = routeNum.ToUpper() };
            
            var response = await client.PostAsJsonAsync(MtrBusApiUrl, requestBody);
            var mtrData = await response.Content.ReadFromJsonAsync<MtrBusResponse>();
            
            if (mtrData?.RouteStops == null) return [];
            var stopData = mtrData.RouteStops.FirstOrDefault(s => s.BusStopId == stopId);
            if (stopData?.BusEtas == null) return [];

            var results = new List<BusEtaDto>();
            int seq = 1;

            foreach (var eta in stopData.BusEtas)
            {
                DateTime? estimatedTime = null;
                if (int.TryParse(eta.DepartureTimeInSecond, out int seconds) && seconds > 0)
                {
                    estimatedTime = DateTime.UtcNow.AddSeconds(seconds);
                }

                var parts = stopId.Split('-');
                string dirCode = (parts.Length > 1 && parts[1].Length >= 1) ? parts[1].Substring(0, 1).ToUpper() : "O";
                string mappedDir = dirCode == "D" ? "I" : "O";

                results.Add(new BusEtaDto(routeNum, mappedDir, seq++, stopId, "終點站", estimatedTime, eta.DepartureTimeText ?? "", ""));
            }

            return results.OrderBy(e => e.EtaTime ?? DateTime.MaxValue).ToList();
        }
        catch { return []; }
    }
    
    private async Task<List<BusEtaDto>> GetNlbEtasAsync(string stopId, string nlbRouteId, string routeNum)
    {
        try
        {
            var client = GetStandardClient();
            string url = $"{NlbApiUrl}?action=estimatedArrivals&routeId={Uri.EscapeDataString(nlbRouteId)}&stopId={Uri.EscapeDataString(stopId)}&language=1";
        
            var nlbData = await client.GetFromJsonAsync<NlbEtaResponse>(url);
            if (nlbData?.Etas == null) return [];

            var results = new List<BusEtaDto>();
            int seq = 1;

            foreach (var eta in nlbData.Etas)
            {
                DateTime? arrival = DateTime.TryParse(eta.ArrivalTime, out var dt) ? dt : null;
            
                results.Add(new BusEtaDto(routeNum, "O", seq++, stopId, eta.VariantName ?? "嶼巴總站", arrival, eta.Departed == "1" ? "已離開" : "", ""));
            }
            return results.OrderBy(e => e.EtaTime ?? DateTime.MaxValue).ToList();
        }
        catch { return []; }
    }
    
    private async Task<List<BusEtaDto>> GetGmbEtasAsync(string stopId, string gmbRouteId, string routeNum)
    {
        try
        {
            var client = GetStandardClient();
            string url = $"{GmbBaseUrl}/eta/stop/{Uri.EscapeDataString(stopId)}";
            
            var response = await client.GetFromJsonAsync<GmbEtaResponse>(url);
            if (response?.Data == null) return [];

            var results = new List<BusEtaDto>();
            int seq = 1;

            foreach (var group in response.Data)
            {
                if (group.RouteId.ToString() != gmbRouteId) continue;

                string mappedDir = group.RouteSeq == 1 ? "O" : "I";

                foreach (var eta in group.Etas)
                {
                    // FIXED: Replaced named arguments with purely positional arguments to avoid casing compiler errors
                    results.Add(new BusEtaDto(
                        routeNum, 
                        mappedDir, 
                        seq++, 
                        stopId, 
                        "目的地", 
                        eta.EtaTime, 
                        eta.RemarksTc ?? "", 
                        eta.RemarksEn ?? ""
                    ));
                }
            }
            return results.OrderBy(e => e.EtaTime ?? DateTime.MaxValue).ToList();
        }
        catch { return []; }
    }

    // --- Green Minibus Private Helpers for Sync Jobs ---
    
    private async Task<List<BusRouteDto>> GetGmbRoutesAsync(string region)
    {
        try
        {
            var client = GetStandardClient();
            string url = string.IsNullOrEmpty(region) ? $"{GmbBaseUrl}/route" : $"{GmbBaseUrl}/route/{Uri.EscapeDataString(region)}";
            var response = await client.GetFromJsonAsync<GmbRouteListResponse>(url);
            if (response?.Data == null) return [];

            var allCodes = new List<string>();
            if (string.IsNullOrEmpty(region)) {
                allCodes.AddRange(response.Data.Hki); allCodes.AddRange(response.Data.Kln); allCodes.AddRange(response.Data.Nt);
            } else {
                allCodes = region.ToUpper() switch { "HKI" => response.Data.Hki, "KLN" => response.Data.Kln, _ => response.Data.Nt };
            }

            return allCodes.Select(c => new BusRouteDto(c, null, null, null, null, null, null, null, "GMB")).ToList();
        }
        catch { return []; }
    }

    private async Task<List<BusRouteStopDto>> GetGmbRouteStopsAsync(string routeId, string routeSeq)
    {
        try
        {
            var client = GetStandardClient();
            string url = $"{GmbBaseUrl}/route-stop/{Uri.EscapeDataString(routeId)}/{Uri.EscapeDataString(routeSeq)}";
            var response = await client.GetFromJsonAsync<GmbRouteStopResponse>(url);
            
            // FIXED: Adjusted to exactly 5 positional arguments to match your BusRouteStopDto signature
            // (Typically: route, bound, serviceType, seq, stopId)
            return response?.Data?.RouteStops?.Select(s => new BusRouteStopDto(
                routeId, 
                routeSeq, 
                null, 
                s.StopSeq, 
                s.StopId.ToString()
            )).ToList() ?? [];
        }
        catch { return []; }
    }

    private async Task<BusStopDto?> GetGmbStopDetailsAsync(string stopId)
    {
        try
        {
            var client = GetStandardClient();
            string url = $"{GmbBaseUrl}/stop/{Uri.EscapeDataString(stopId)}";
            var response = await client.GetFromJsonAsync<GmbStopDetailResponse>(url);
            if (response?.Data == null) return null;

            return new BusStopDto(
                response.Data.StopId.ToString(),
                response.Data.NameTc, 
                response.Data.NameEn,
                response.Data.Coordinates.Wgs84.Latitude.ToString(),
                response.Data.Coordinates.Wgs84.Longitude.ToString()
            );
        }
        catch { return null; }
    }
}