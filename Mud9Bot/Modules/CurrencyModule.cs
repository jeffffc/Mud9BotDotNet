using System.Text.RegularExpressions;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Modules;

public class CurrencyModule(ICurrencyService currencyService)
{
    // Regex: 支援 "123.45 USD to HKD" 或 "1,000 jpy TO usd"
    [TextTrigger(@"^[\s]*(?<amount>[0-9,.]+)\s*(?<a>[A-Za-z]{3})\s+[tT][oO]\s+(?<b>[A-Za-z]{3})$", Description = "Currency converter")]
    public async Task HandleCurrencyConvertAsync(ITelegramBotClient bot, Message message, Match match, CancellationToken ct)
    {
        string amountStr = match.Groups["amount"].Value.Replace(",", "");
        string from = match.Groups["a"].Value.ToUpper();
        string to = match.Groups["b"].Value.ToUpper();

        if (!double.TryParse(amountStr, out double amount)) return;

        var result = currencyService.Convert(amount, from, to);

        if (result.Success)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: $"💰 <b>匯率轉換結果</b>\n\n{result.Message}",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: ct
            );
        }
        else if (message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
        {
            // 私訊時才報錯貨幣代號不對，群組中靜默處理避免誤觸
            await bot.Reply(message, result.Message, ct: ct);
        }
    }
}