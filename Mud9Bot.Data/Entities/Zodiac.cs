using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("zodiacs")]
public class Zodiac
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("date_key")] // yyyy-MM-dd
    public string DateKey { get; set; } = string.Empty;

    [Column("zodiac_index")]
    public int ZodiacIndex { get; set; }

    [Column("summary")]
    public string Summary { get; set; } = string.Empty;

    // Categories
    public int OverallScore { get; set; }
    public string OverallText { get; set; } = string.Empty;

    public int LoveScore { get; set; }
    public string LoveText { get; set; } = string.Empty;

    public int CareerScore { get; set; }
    public string CareerText { get; set; } = string.Empty;

    public int MoneyScore { get; set; }
    public string MoneyText { get; set; } = string.Empty;
}