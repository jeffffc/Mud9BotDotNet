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

    [Column("amount")]
    public int Amount { get; set; }

    [Column("time")]
    public DateTime Time { get; set; }

    [Column("telegram_payment_charge_id")]
    public string? TelegramPaymentChargeId { get; set; }

    [Column("provider_payment_charge_id")]
    public string? ProviderPaymentChargeId { get; set; }
}