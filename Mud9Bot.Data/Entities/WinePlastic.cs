using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("wineplastic")]
public class WinePlastic
{
    [Key]
    [Column("wpid")]
    public int WinePlasticId { get; set; }

    [Column("groupid")]
    public int GroupId { get; set; }

    [Column("userid")]
    public int UserId { get; set; }

    [Column("wine")]
    public int Wine { get; set; }

    [Column("plastic")]
    public int Plastic { get; set; }

    [Column("givenby")]
    public int GivenBy { get; set; }

    [Column("timeadded")]
    public DateTime TimeAdded { get; set; }

    [Column("disabled")]
    public int Disabled { get; set; }

    [Column("disableddate")]
    public DateTime? DisabledDate { get; set; }
}