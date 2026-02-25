using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("admin_audit_logs")]
public class AdminAuditLog
{
    [Key]
    public int Id { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Column("admin_id")]
    public long AdminId { get; set; }

    [Column("admin_name")]
    public string AdminName { get; set; } = string.Empty;

    [Column("action")]
    public string Action { get; set; } = string.Empty; // e.g., "CHANGE_SETTING", "BAN_USER"

    [Column("details")]
    public string? Details { get; set; } // e.g., "maintenance -> true"

    [Column("ip_address")]
    public string? IpAddress { get; set; }
}