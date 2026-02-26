using System;
using System.Text.Json.Serialization;

namespace Mud9Bot.Transport.Models;

/// <summary>
/// Data models for deserializing responses from the official MTR Next Train API.
/// 港鐵 Next Train API 嘅資料結構。
/// </summary>
public record MtrScheduleResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sys_time")] string SysTime,
    [property: JsonPropertyName("isdelay")] string IsDelay,
    // The key here is dynamic (e.g. "TKL-TUC"), so we must use a Dictionary to capture it
    // 港鐵 API 嘅 Key 係動態嘅 (例如 "TKL-TUC")，所以一定要用 Dictionary 接住
    [property: JsonPropertyName("data")] Dictionary<string, MtrLineStationData>? Data
);

public record MtrLineStationData(
    [property: JsonPropertyName("curr_time")] string CurrTime,
    [property: JsonPropertyName("sys_time")] string SysTime,
    [property: JsonPropertyName("UP")] List<MtrTrainEta>? Up,
    [property: JsonPropertyName("DOWN")] List<MtrTrainEta>? Down
);

public record MtrTrainEta(
    [property: JsonPropertyName("ttnt")] string TimeToNextTrain, // e.g., "1" (mins) or "" (arriving/departing)
    [property: JsonPropertyName("valid")] string Valid,
    [property: JsonPropertyName("plat")] string Platform,
    [property: JsonPropertyName("time")] string Time, // e.g., "2026-02-27 01:52:00"
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("dest")] string Destination,
    [property: JsonPropertyName("seq")] string Sequence
);

// Models for our internal static topology (since MTR doesn't have a /route endpoint)
public record MtrLineDto(string LineCode, string NameTc, string NameEn, string ColorCode);
public record MtrStationDto(string StationCode, string NameTc, string NameEn);