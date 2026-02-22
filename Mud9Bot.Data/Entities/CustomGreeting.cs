using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("custom_greetings")]
public class CustomGreeting
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("telegram_id")]
    public long TelegramId { get; set; }

    /// <summary>
    /// Type of greeting: "MORNING" or "NIGHT"
    /// </summary>
    [Column("greeting_type")]
    public string GreetingType { get; set; } = "MORNING";

    /// <summary>
    /// The actual text message for the greeting
    /// </summary>
    [Column("content")]
    public string Content { get; set; } = string.Empty;
}