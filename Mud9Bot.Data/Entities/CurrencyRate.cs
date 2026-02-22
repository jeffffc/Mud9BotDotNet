using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("currency_rates")]
public class CurrencyRate
{
    [Key]
    [Column("code")] // 貨幣代號，例如 USD, HKD, JPY
    public string Code { get; set; } = string.Empty;

    [Column("rate")] // 原本對歐元 (EUR) 的匯率 (1 EUR = X)
    public double Rate { get; set; }

    [Column("rate_hkd")] // 新增：對港幣 (HKD) 的匯率 (1 HKD = X)
    public double RateHkd { get; set; }

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}