using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("dailylimit")]
public class DailyLimit
{
    [Key]
    [Column("limitid")]
    public int LimitId { get; set; }

    [Column("userid")]
    public int UserId { get; set; }

    [Column("groupid")]
    public int GroupId { get; set; }

    [Column("wlimit")]
    public int WineLimit { get; set; }

    [Column("plimit")]
    public int PlasticLimit { get; set; }
}