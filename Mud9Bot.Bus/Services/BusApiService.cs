using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Mud9Bot.Bus.Interfaces;
using Mud9Bot.Bus.Models;

namespace Mud9Bot.Bus.Services;

/// <summary>
/// Service to fetch data from official Bus APIs.
/// Updated for Citybus V2 specifications: Stop API is unified (no company in path).
/// Supports specific base paths for KMB and LWB.
/// </summary>
public class BusApiService(IHttpClientFactory httpClientFactory, IMemoryCache cache) : IBusApiService
{
    private const string KmbBaseUrl = "https://data.etabus.gov.hk/v1/transport/kmb/";
    private const string LwbBaseUrl = "https://data.etabus.gov.hk/v1/transport/lwb/";
    private const string CitybusBaseUrl = "https://rt.data.gov.hk/v2/transport/citybus/";

    private (HttpClient Client, string BaseUrl) GetClient(string company)
    {
        var client = httpClientFactory.CreateClient();
        string c = company.ToUpper();
        
        // Handling specific base URLs for KMB and LWB as per official documentation
        // 根據官方文件，九巴同龍運雖然係同一集團，但 API Path 係分開嘅。
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
        return response?.Data ?? [];
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
        return response?.Data ?? [];
    }
}