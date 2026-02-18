using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("users")]
public class BotUser
{
    [Key]
    [Column("userid")] // Maps to legacy 'userid'
    public int Id { get; set; }

    [Column("telegramid")]
    public long TelegramId { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    public string? LastName { get; set; }

    [Column("username")]
    public string? Username { get; set; }
    
    // New fields from Legacy Schema
    [Column("plastic")]
    public int Plastic { get; set; }

    [Column("wine")]
    public int Wine { get; set; }

    [Column("timeadded")]
    public DateTime TimeAdded { get; set; } = DateTime.UtcNow;

    // Metrics for the new bot (kept separate from legacy timeadded)
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}