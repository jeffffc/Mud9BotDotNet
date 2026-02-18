using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Mud9Bot.Modules;

public class FortuneModule(IFortuneService fortuneService)
{
    [Command("fortune")]
    public async Task Fortune(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var (fortune, index) = fortuneService.GetRandomFortune();

        var keyboard = new InlineKeyboardMarkup(
            // Changed label to Cantonese "解籤" (Explain Fortune)
            InlineKeyboardButton.WithCallbackData("解籤", $"fortune:{index}")
        );

        // Standard Telegram.Bot method (Latest Version)
        // Uses ReplyParameters to reply to the specific message
        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: fortune.a,
            replyMarkup: keyboard,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
}