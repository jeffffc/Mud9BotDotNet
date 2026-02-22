using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mud9Bot.Data.Entities;

[Table("donation")]
public class Donation
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("donationid")]
    public string? DonationId { get; set; }

    [Column("telegramid")]
    public long TelegramId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    /// <summary>
    /// 原有的金額欄位 (例如用於法幣支付)
    /// </summary>
    [Column("amount")]
    public int Amount { get; set; }

    /// <summary>
    /// 新增的 Telegram Stars 欄位 (XTR)
    /// </summary>
    [Column("stars")]
    public int Stars { get; set; }

    [Column("time")]
    public DateTime Time { get; set; } = DateTime.UtcNow;

    [Column("telegram_payment_charge_id")]
    public string? TelegramPaymentChargeId { get; set; }

    [Column("provider_payment_charge_id")]
    public string? ProviderPaymentChargeId { get; set; }
}