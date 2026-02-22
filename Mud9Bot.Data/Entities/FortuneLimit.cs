using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("fortune_limit")]
public class FortuneLimit
{
    [Key]
    [Column("ftid")]
    public int FortuneLimitId { get; set; }

    [Column("groupid")]
    public int GroupId { get; set; }

    [Column("userid")]
    public int UserId { get; set; }

    [Column("last_date")]
    public DateTime LastDate { get; set; }

    [Column("msgid")]
    public int MessageId { get; set; }
}