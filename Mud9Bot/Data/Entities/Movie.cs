using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("movies")]
public class Movie
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Rating { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Writer { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public string Starring { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;

    [Column("on_show_date")]
    public string OnShowDate { get; set; } = string.Empty; // 新增欄位
    
    [Column("is_showing")]
    public bool IsShowing { get; set; } = true;

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}