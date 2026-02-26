using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities.Bus;

/// <summary>
/// Entity for Bus Routes (KMB/Citybus) with snake_case mapping for PSQL.
/// 巴士路線實體（九巴/城巴），加咗 snake_case alias 方便用落 PSQL。
/// </summary>
[Table("bus_routes")]
public class BusRoute
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = null!; // Format: {Company}_{Route}_{Bound}_{ServiceType}

    [Column("company")]
    public string Company { get; set; } = null!; // KMB / Citybus

    [Column("route_number")]
    public string RouteNumber { get; set; } = null!;

    [Column("bound")]
    public string Bound { get; set; } = null!; // KMB: I/O, Citybus: inbound/outbound

    [Column("service_type")]
    public string ServiceType { get; set; } = null!;
    
    [Column("origin_tc")]
    public string OriginTc { get; set; } = null!;

    [Column("origin_en")]
    public string OriginEn { get; set; } = null!;

    [Column("destination_tc")]
    public string DestinationTc { get; set; } = null!;

    [Column("destination_en")]
    public string DestinationEn { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation Property: One route has many stops
    // 導航屬性：一條線有好多個站
    public virtual ICollection<BusRouteStop> RouteStops { get; set; } = new List<BusRouteStop>();
}