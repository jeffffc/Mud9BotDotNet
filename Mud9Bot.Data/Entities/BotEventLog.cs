using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mud9Bot.Data.Entities;

[Table("bot_event_logs")]
[Index(nameof(EventType), nameof(Metadata), nameof(ChatType), IsUnique = true)]
public class BotEventLog
{
    [Key]
    [Column("id")] // Matches the DB column name
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")] // This fixes the "does not exist" error
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("metadata")]
    public string Metadata { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("chat_type")]
    public string ChatType { get; set; } = string.Empty;

    [Column("count")]
    public long Count { get; set; } = 0;
}