using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("command_logs")]
public class CommandLog
{
    [Key]
    public int Id { get; set; }

    public long UserId { get; set; }
    public long ChatId { get; set; } // 0 if private
    public string Command { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}