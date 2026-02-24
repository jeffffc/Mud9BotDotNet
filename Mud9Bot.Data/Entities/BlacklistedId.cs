using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("blacklist")]
public class BlacklistedId
{
    [Key]
    [Column("telegram_id")]
    public long TelegramId { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("banned_by")]
    public long BannedBy { get; set; }

    [Column("time_added")]
    public DateTime TimeAdded { get; set; } = DateTime.UtcNow;
}