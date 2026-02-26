using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities.Bus;

/// <summary>
/// Junction entity for Route-Stop mapping with snake_case mapping for PSQL.
/// 路線同站點嘅關聯實體，加咗 snake_case alias 方便用落 PSQL。
/// </summary>
[Table("bus_route_stops")]
public class BusRouteStop
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = null!; // Format: {RouteId}_{Sequence}

    [ForeignKey("BusRoute")]
    [Column("route_id")]
    public string RouteId { get; set; } = null!;
    public virtual BusRoute BusRoute { get; set; } = null!;

    [ForeignKey("BusStop")]
    [Column("stop_id")]
    public string StopId { get; set; } = null!;
    public virtual BusStop BusStop { get; set; } = null!;

    [Column("sequence")]
    public int Sequence { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}