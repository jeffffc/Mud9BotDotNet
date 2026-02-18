using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Services.Interfaces;

using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Extensions;

namespace Mud9Bot.Modules;

public class WinePlasticModule(IWinePlasticService wpService, IUserService userService)
{
    [Command("z")]
    public async Task ZCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. Validation: Must be a reply in group
        if (message.Chat.Type == ChatType.Private)
        {
            await bot.Reply(message, "呢度用唔到，要群組先用到 `/z`。", ct);
            return;
        }
        if (message.ReplyToMessage == null)
        {
            await bot.Reply(message, "你想賜酒 🍻 ／派膠 🌚 俾邊個呀？對住佢用 `/z` 啦！", ct);
            return;
        }

        var sender = message.From;
        var target = message.ReplyToMessage.From;

        if (sender != null) await userService.SyncUserAsync(sender, ct);
        if (target != null) await userService.SyncUserAsync(target, ct);

        // 2. Validation: No self-voting
        if (sender!.Id == target!.Id)
        {
            await bot.Reply(message, "想賜酒俾自己？你諗多咗。", ct);
            return;
        }
        
        int num = 1; // default value = 1
        
        // 3. Check wine/plastic amount
        
        if (args.Length > 0 && args[0].All(char.IsDigit)) // args exist and first arg is a number
        {
            num = int.Parse(args[0]);
            if (num is < 1 or > 20)
            {
                await bot.Reply(message, "最少係 1 最多去到 20！", ct);
                return;
            }
        }
        
        // 4. Construct Buttons
        // Callback Format: "wine+{senderId}+{targetId}+number" or "plastic+{senderId}+{targetId}+number"
        // We need senderId to verify the clicker is the original commander.
        
        var keyboard = new InlineKeyboardMarkup(new [] {
                InlineKeyboardButton.WithCallbackData(num == 1 ? "賜酒 🍻" : $"賜 {num} 杯酒 🍻", $"wine+{sender.Id}+{target.Id}+{num}"),
                InlineKeyboardButton.WithCallbackData(num == 1 ? "派膠 🌚" : $"派 {num} 粒膠 🌚", $"plastic+{sender.Id}+{target.Id}+{num}")
            }
        );

        await bot.SendMessage(
            message.Chat.Id,
            $"你想對【{Markdown.Escape(target.FirstName)}】賜酒 🍻 定派膠 🌚?",
            replyMarkup: keyboard,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct
        );
    }
    
    [CallbackQuery("wine")]
    public async Task HandleWinePlasticCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        await HandleWinePlastic(bot, query, true, ct);
    }
    
    [CallbackQuery("plastic")]
    public async Task HandlePlasticCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        await HandleWinePlastic(bot, query, true, ct);
    }

    private async Task HandleWinePlastic(ITelegramBotClient bot, CallbackQuery query, bool isWine, CancellationToken ct)
    {
        // Format: "wine+{senderId}+{targetId}+number" 
        var parts = query.Data!.Split('+');
        if (parts.Length != 4) return;

        if (!long.TryParse(parts[1], out var originalSenderId)) return;
        if (!long.TryParse(parts[2], out var targetTelegramId)) return;
        if (!int.TryParse(parts[3], out var num)) return;
        
        
        // 1. Verify Clicker is the Original Sender
        if (query.From.Id != originalSenderId)
        {
            await bot.AnswerCallbackQuery(query.Id, Constants.NoOriginalSenderMessageList.GetAny(), cancellationToken: ct);
            return;
        }

        // 2. Map Telegram IDs to Internal DB Entities
        var result = await wpService.ProcessTransactionByTelegramIdAsync(
            originalSenderId,
            targetTelegramId,
            query.Message!.Chat.Id,
            isWine,
            num
        );

        if (result.Item1)
        {
            // 4. Respond
            await bot.EditMessageText(
                chatId: query.Message.Chat.Id,
                messageId: query.Message.MessageId,
                text: result.Item2,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct
            );
        }
        else
        {
            // change to popup?
            await bot.EditMessageText(
                chatId: query.Message.Chat.Id,
                messageId: query.Message.MessageId,
                text: result.Item2,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct
            );
        }
    }
    
    [Command("check")]
    public async Task CheckCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. Group Check
        if (message.Chat.Type == ChatType.Private)
        {
            await bot.SendMessage(message.Chat.Id, "呢度用唔到，要群組先用到 `/check`。", replyParameters: new ReplyParameters { MessageId = message.MessageId }, cancellationToken: ct);
            return;
        }

        // 2. Send "Please wait" message
        var sentMessage = await bot.SendMessage(
            chatId: message.Chat.Id,
            text: "請等等，我繽Ｘ樂緊……",
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );

        try
        {
            var user = message.From!;
            var chatTitle = message.Chat.Title ?? "Group";

            // 3. Get Stats
            var (wine, plastic, wLimit, pLimit) = await wpService.GetPersonalStatsAsync(user.Id, message.Chat.Id);

            // 4. Format Message (Using HTML as per legacy logic for name safety)
            var fullName = user.FirstName + (user.LastName != null ? " " + user.LastName : "");
            
            // Simple HTML escape helpers
            string EscapeHtml(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            
            var safeName = EscapeHtml(fullName);
            var safeTitle = EscapeHtml(chatTitle);

            var checkMsg = $"<b>【{safeName}】</b>，喺<b>【{safeTitle}】</b>呢度︰\n" +
                           $"你收過 <code>{wine}</code> 杯酒同 <code>{plastic}</code> 粒膠；\n" +
                           $"今日派剩 <code>{wLimit}</code> 杯酒同 <code>{pLimit}</code> 粒膠。";

            // 5. Edit Message
            await bot.EditMessageText(
                chatId: message.Chat.Id,
                messageId: sentMessage.MessageId,
                text: checkMsg,
                parseMode: ParseMode.Html,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            var errorMsg = "搞唔掂，請你再試過，再唔得就幫我同管理員講。";
            await bot.EditMessageText(
                chatId: message.Chat.Id,
                messageId: sentMessage.MessageId,
                text: errorMsg,
                cancellationToken: ct
            );
            // In a real scenario, you might want to log 'ex' using injected ILogger if available
        }
    }
}