using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("user_feedback")]
public class UserFeedback
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("telegram_id")]
    public long TelegramId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    [Column("is_resolved")]
    public bool IsResolved { get; set; } = false;

    [Column("admin_reply")]
    public string? AdminReply { get; set; }
}