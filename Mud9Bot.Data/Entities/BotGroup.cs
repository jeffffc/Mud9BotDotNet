using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("groups")]
public class BotGroup
{
    [Key]
    [Column("groupid")] // Maps to legacy 'groupid'
    public int Id { get; set; }

    [Column("telegramid")]
    public long TelegramId { get; set; }

    [Column("name")] // Legacy column was 'name'
    public string Title { get; set; } = string.Empty;

    [Column("username")]
    public string? Username { get; set; }
    
    [Column("wquota")]
    public int WQuota { get; set; } = 5;

    [Column("pquota")]
    public int PQuota { get; set; } = 5;

    // --- New Fields from Legacy Schema ---

    [Column("welcome")]
    public long Welcome { get; set; } // Legacy was bigint

    [Column("welcomegif")]
    public string? WelcomeGif { get; set; }

    [Column("pinned_id")]
    public int PinnedId { get; set; }

    [Column("timeadded")]
    public DateTime TimeAdded { get; set; } = DateTime.UtcNow;

    // Settings (Legacy tinyint mapped to int)
    [Column("offfortune")]
    public bool OffFortune { get; set; }

    [Column("offzodiac")]
    public bool OffZodiac { get; set; }

    [Column("offlomo")]
    public bool OffLomo { get; set; }

    [Column("offsimp")]
    public bool OffSimp { get; set; }
}