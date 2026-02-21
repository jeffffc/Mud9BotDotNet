using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("lomo_ignore_words")]
public class LomoIgnoreWord
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column("word")]
    public string Word { get; set; } = string.Empty;
}