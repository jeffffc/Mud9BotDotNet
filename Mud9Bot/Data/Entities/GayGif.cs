using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("gaygif")]
public class GayGif
{
    [Key]
    [Column("gaygifid")]
    public int GayGifId { get; set; }

    [Column("fileid")]
    public required string FileId { get; set; }
}