using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mud9Bot.Data.Entities;

[Table("bot_event_logs")]
// We define a unique index on the combination of these three fields to support the UPSERT logic
[Index(nameof(EventType), nameof(Metadata), nameof(ChatType), IsUnique = true)]
public class BotEventLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty; // e.g., 'system', 'command'

    [Required]
    [MaxLength(100)]
    public string Metadata { get; set; } = string.Empty;  // e.g., 'total_volume', 'z'

    [Required]
    [MaxLength(50)]
    public string ChatType { get; set; } = string.Empty;  // e.g., 'private', 'supergroup'

    public long Count { get; set; } = 0;
}