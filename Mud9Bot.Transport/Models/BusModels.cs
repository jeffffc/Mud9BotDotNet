using System;
using System.Text.Json.Serialization;

namespace Mud9Bot.Transport.Models;

/// <summary>
/// Data models for deserializing responses from KMB and Citybus APIs.
/// 支援 KMB V1 同 Citybus V2 嘅 API 數據模型。
/// </summary>
public record BusApiResponse<T>(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("generated_timestamp")] DateTime GeneratedTimestamp,
    [property: JsonPropertyName("data")] T Data
);

public record BusRouteDto(
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("bound")] string? Bound, // KMB 專用
    [property: JsonPropertyName("dir")] string? Dir,     // Citybus V2 專用
    [property: JsonPropertyName("service_type")] string? ServiceType,
    [property: JsonPropertyName("orig_tc")] string? OriginTc,
    [property: JsonPropertyName("orig_en")] string? OriginEn,
    [property: JsonPropertyName("dest_tc")] string? DestinationTc,
    [property: JsonPropertyName("dest_en")] string? DestinationEn,
    [property: JsonPropertyName("co")] string? CompanyId // Citybus V2 (CTB/NWFB)
);

public record BusRouteStopDto(
    [property: JsonPropertyName("co")] string? Company,
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("dir")] string? Dir,
    [property: JsonPropertyName("seq")] int Sequence,    // V2 改為整數
    [property: JsonPropertyName("stop")] string StopId
);

public record BusStopDto(
    [property: JsonPropertyName("stop")] string StopId,
    [property: JsonPropertyName("name_tc")] string NameTc,
    [property: JsonPropertyName("name_en")] string NameEn,
    [property: JsonPropertyName("lat")] string? Latitude,
    [property: JsonPropertyName("long")] string? Longitude
);

public record BusEtaDto(
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("dir")] string Direction,
    [property: JsonPropertyName("seq")] int Sequence,
    [property: JsonPropertyName("stop")] string StopId,
    [property: JsonPropertyName("dest_tc")] string DestinationTc,
    [property: JsonPropertyName("eta")] DateTime? EtaTime,   // Actual arrival time
    [property: JsonPropertyName("rmk_tc")] string RemarkTc, // Remarks like "Scheduled" or "Last Bus"
    [property: JsonPropertyName("rmk_en")] string RemarkEn
);

// =========================================================================
// MTR BUS SPECIFIC MODELS (UPDATED FOR CORRECT JSON SCHEMA)
// =========================================================================

public record MtrBusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("busStop")] List<MtrBusRouteStop>? RouteStops
);

public record MtrBusRouteStop(
    [property: JsonPropertyName("busStopId")] string BusStopId,
    // 修正：JSON 裡面陣列名係 "bus"，唔係 "busEta"
    [property: JsonPropertyName("bus")] List<MtrBusEta>? BusEtas 
);

public record MtrBusEta(
    // 修正：MTR 提供秒數同文字，而唔係確實 DateTime 字串
    [property: JsonPropertyName("departureTimeInSecond")] string DepartureTimeInSecond, 
    [property: JsonPropertyName("departureTimeText")] string DepartureTimeText,
    [property: JsonPropertyName("busRemark")] string? BusRemark
);