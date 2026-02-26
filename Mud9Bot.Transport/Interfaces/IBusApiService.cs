using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Mud9Bot.Transport.Models;

namespace Mud9Bot.Transport.Interfaces;

public interface IBusApiService
{
    Task<List<BusRouteDto>> GetRoutesAsync(string company, string routeNum = "");
    Task<List<BusRouteStopDto>> GetRouteStopsAsync(string company, string routeNum, string bound, string serviceType);
    Task<BusStopDto?> GetStopDetailsAsync(string company, string stopId);
    Task<List<BusEtaDto>> GetEtasAsync(string company, string stopId, string routeNum, string serviceType);
}