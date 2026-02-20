using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;

namespace Mud9Bot.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Handles the pre-checkout query to approve or reject the payment.
    /// </summary>
    Task HandlePreCheckoutQueryAsync(ITelegramBotClient bot, PreCheckoutQuery query, CancellationToken ct);

    /// <summary>
    /// Processes the successful payment, saves to database, and notifies logs.
    /// </summary>
    Task HandleSuccessfulPaymentAsync(ITelegramBotClient bot, Message message, SuccessfulPayment payment, CancellationToken ct);
}