using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("job")]
public class Job
{
    [Key]
    [Column("jobid")]
    public int JobId { get; set; }

    [Column("timeadded")]
    public DateTime TimeAdded { get; set; }

    [Column("chatid")]
    public long ChatId { get; set; }

    [Column("telegramid")]
    public long TelegramId { get; set; } // Changed from int to long for safety

    [Column("name")]
    public string? Name { get; set; }

    [Column("msgid")]
    public int MessageId { get; set; }

    [Column("time")]
    public DateTime Time { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}