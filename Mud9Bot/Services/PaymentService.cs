using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Mud9Bot.Services;

public class PaymentService(
    IServiceScopeFactory scopeFactory, 
    ILogger<PaymentService> logger, 
    IConfiguration configuration) : IPaymentService
{
    private readonly long _logGroupId = configuration.GetValue<long>("BotConfiguration:LogGroupId");
    private readonly HashSet<long> _devIds = configuration.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? new HashSet<long>();
    
    public async Task HandlePreCheckoutQueryAsync(ITelegramBotClient bot, PreCheckoutQuery query, CancellationToken ct)
    {
        // For donations, we approve the request to allow the payment to proceed.
        await bot.AnswerPreCheckoutQuery(query.Id, cancellationToken: ct);
    }

    public async Task HandleSuccessfulPaymentAsync(ITelegramBotClient bot, Message message, SuccessfulPayment payment, CancellationToken ct)
    {
        var user = message.From;
        logger.LogInformation("Successful Stars payment: {Amount} from {UserId}", payment.TotalAmount, user?.Id);

        int donationId = 0;

        using (var donationScope = scopeFactory.CreateScope())
        {
            var db = donationScope.ServiceProvider.GetRequiredService<BotDbContext>();
            
            var donation = new Donation
            {
                TelegramId = user?.Id ?? 0,
                Name = (user?.FirstName + " " + user?.LastName).Trim(),
                Username = user?.Username,
                Stars = (int)payment.TotalAmount,
                TelegramPaymentChargeId = payment.TelegramPaymentChargeId,
                ProviderPaymentChargeId = payment.ProviderPaymentChargeId,
                Time = DateTime.UtcNow
            };
            
            db.Set<Donation>().Add(donation);
            await db.SaveChangesAsync(ct);
            donationId = donation.Id;
        }

        string successMsg = $"多謝支持，不勝感激\nReference ID: #Mud9Bot{donationId}";
        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: successMsg,
            cancellationToken: ct);

        if (_logGroupId != 0)
        {
            string adminLog = $"💰 <b>收到 Stars 捐款！</b>\n" +
                             $"👤 <b>用戶：</b> <a href=\"tg://user?id={user?.Id}\">{user?.FirstName.EscapeHtml()}</a> (<code>{user?.Id}</code>)\n" +
                             $"⭐ <b>金額：</b> {payment.TotalAmount} Stars\n" +
                             $"🆔 <b>編號：</b> #Mud9Bot{donationId}\n" +
                             $"💳 <b>交易 ID：</b> <code>{payment.TelegramPaymentChargeId}</code>\n" +
                             $"🕒 <b>時間：</b> <code>{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</code>";
            long[] devIds = _devIds.ToArray();
            await bot.SendMessage(devIds[0], adminLog, parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }
}