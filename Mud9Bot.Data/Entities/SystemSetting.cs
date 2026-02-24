using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("system_settings")]
public class SystemSetting
{
    [Key]
    [Column("setting_key")]
    public string SettingKey { get; set; } = null!;
    
    [Column("setting_value")]
    public string SettingValue { get; set; } = string.Empty;
    
    [Column("description")]
    public string? Description { get; set; }
    
    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}