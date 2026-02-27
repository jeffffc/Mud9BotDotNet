using System.Text.Json.Serialization;

namespace Mud9Bot.Transport.Models;

// 1. Route List (By Region)
public record GmbRouteListResponse(
    [property: JsonPropertyName("data")] GmbRegionData Data
);

public record GmbRegionData(
    [property: JsonPropertyName("HKI")] List<string> Hki,
    [property: JsonPropertyName("KLN")] List<string> Kln,
    [property: JsonPropertyName("NT")] List<string> Nt
);

// 2. Route Details (Specific variants for a route number)
public record GmbRouteDetailResponse(
    [property: JsonPropertyName("data")] List<GmbRouteVariantDto> Data
);

public record GmbRouteVariantDto(
    [property: JsonPropertyName("route_id")] int RouteId,
    [property: JsonPropertyName("region")] string Region,
    [property: JsonPropertyName("route_code")] string RouteCode,
    [property: JsonPropertyName("description_tc")] string DescriptionTc,
    [property: JsonPropertyName("description_en")] string DescriptionEn,
    [property: JsonPropertyName("directions")] List<GmbDirectionDto> Directions
);

public record GmbDirectionDto(
    [property: JsonPropertyName("route_seq")] int RouteSeq, // 1 = Outbound, 2 = Inbound
    [property: JsonPropertyName("orig_tc")] string OriginTc,
    [property: JsonPropertyName("dest_tc")] string DestinationTc,
    [property: JsonPropertyName("orig_en")] string OriginEn,
    [property: JsonPropertyName("dest_en")] string DestinationEn
);

// 3. Stop Sequence for a specific variant
public record GmbRouteStopResponse(
    [property: JsonPropertyName("data")] GmbStopSequenceContainer Data
);

public record GmbStopSequenceContainer(
    [property: JsonPropertyName("route_stops")] List<GmbStopSeqDto> RouteStops
);

public record GmbStopSeqDto(
    [property: JsonPropertyName("stop_seq")] int StopSeq,
    [property: JsonPropertyName("stop_id")] int StopId,
    [property: JsonPropertyName("name_tc")] string NameTc,
    [property: JsonPropertyName("name_en")] string NameEn
);

// 4. Physical Stop Details (Coordinates)
public record GmbStopDetailResponse(
    [property: JsonPropertyName("data")] GmbStopDetails Data
);

public record GmbStopDetails(
    [property: JsonPropertyName("stop_id")] int StopId,
    [property: JsonPropertyName("name_tc")] string NameTc,
    [property: JsonPropertyName("name_en")] string NameEn,
    [property: JsonPropertyName("coordinates")] GmbCoordinates Coordinates
);

public record GmbCoordinates(
    [property: JsonPropertyName("wgs84")] GmbWgs84 Wgs84
);

public record GmbWgs84(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude
);

// 5. Live ETA (Updated to include filtering fields)
public record GmbEtaResponse(
    [property: JsonPropertyName("data")] List<GmbEtaGroup> Data
);

public record GmbEtaGroup(
    [property: JsonPropertyName("route_id")] int RouteId,   // Unique internal ID (e.g. 2000001)
    [property: JsonPropertyName("route_seq")] int RouteSeq, // 1 = Outbound, 2 = Inbound
    [property: JsonPropertyName("stop_seq")] int StopSeq,
    [property: JsonPropertyName("eta")] List<GmbEtaItem> Etas
);

public record GmbEtaItem(
    [property: JsonPropertyName("timestamp")] DateTime? EtaTime,
    [property: JsonPropertyName("remarks_tc")] string RemarksTc,
    [property: JsonPropertyName("remarks_en")] string RemarksEn
);