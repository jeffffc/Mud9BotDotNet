using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities.Bus;

/// <summary>
/// Entity for Bus Stops with snake_case mapping for PSQL.
/// 巴士站點實體，加咗 snake_case alias 方便用落 PSQL。
/// </summary>
[Table("bus_stops")]
public class BusStop
{
    [Key]
    [Column("stop_id")]
    public string StopId { get; set; } = null!;
    
    [Column("name_tc")]
    public string NameTc { get; set; } = null!;

    [Column("name_en")]
    public string NameEn { get; set; } = null!;
    
    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation Property: One stop belongs to many routes
    // 導航屬性：一個站可以屬於好多條線
    public virtual ICollection<BusRouteStop> RouteStops { get; set; } = new List<BusRouteStop>();
}