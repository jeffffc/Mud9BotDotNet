using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Services.Interfaces;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Mud9Bot.Modules;

public class WinePlasticModule
{
    [Command("z")]
    public async Task ZCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. Validation: Must be a reply
        if (message.ReplyToMessage == null)
        {
            await bot.SendMessage(message.Chat.Id, "請 Reply 一個訊息黎派酒/膠。", replyParameters: new ReplyParameters { MessageId = message.MessageId }, cancellationToken: ct);
            return;
        }

        var sender = message.From;
        var target = message.ReplyToMessage.From;

        // 2. Validation: No self-voting
        if (sender!.Id == target!.Id)
        {
            await bot.SendMessage(message.Chat.Id, "唔可以派比自己架！", replyParameters: new ReplyParameters { MessageId = message.MessageId }, cancellationToken: ct);
            return;
        }

        // 3. Validation: No bots
        if (target.IsBot)
        {
            await bot.SendMessage(message.Chat.Id, "唔好玩 Bot 啦。", replyParameters: new ReplyParameters { MessageId = message.MessageId }, cancellationToken: ct);
            return;
        }

        // 4. Construct Buttons
        // Callback Format: "wp:{action}:{senderId}:{targetId}"
        // action: "w" (wine) or "p" (plastic)
        // We need senderId to verify the clicker is the original commander.
        
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("賜酒 🍻", $"wp:w:{sender.Id}:{target.Id}"),
                InlineKeyboardButton.WithCallbackData("派膠 🌚", $"wp:p:{sender.Id}:{target.Id}")
            }
        });

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: $"你想對【{target.FirstName}】賜酒 🍻 定派膠 🌚?",
            replyMarkup: keyboard,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
    
    [CallbackQuery("wp")]
    public async Task HandleWinePlasticCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        // Format: "wp:{action}:{senderId}:{targetId}"
        var parts = query.Data!.Split(':');
        if (parts.Length != 4) return;

        var action = parts[1]; // "w" or "p"
        if (!long.TryParse(parts[2], out var originalSenderId)) return;
        if (!long.TryParse(parts[3], out var targetTelegramId)) return;

        // 1. Verify Clicker is the Original Sender
        if (query.From.Id != originalSenderId)
        {
            await bot.AnswerCallbackQuery(query.Id, "你唔係發起人，無權禁掣！", cancellationToken: ct);
            return;
        }

        // 2. Map Telegram IDs to Internal DB Entities
        var senderEntity = await dbContext.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == originalSenderId, ct);
        var targetEntity = await dbContext.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == targetTelegramId, ct);
        var groupEntity = await dbContext.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == query.Message!.Chat.Id, ct);

        if (senderEntity == null || targetEntity == null || groupEntity == null)
        {
            await bot.AnswerCallbackQuery(query.Id, "系統錯誤: 找不到用戶資料 (可能未同步)。", cancellationToken: ct);
            return;
        }

        // 3. Process Transaction
        var result = await wpService.ProcessTransactionAsync(
            senderEntity.Id, 
            targetEntity.Id, 
            groupEntity.Id, 
            action == "w", 
            groupEntity.WQuota, 
            groupEntity.PQuota
        );

        // 4. Respond
        if (result.Contains("用完"))
        {
             await bot.AnswerCallbackQuery(query.Id, result, showAlert: true, cancellationToken: ct);
        }
        else
        {
             await bot.EditMessageText(
                 chatId: query.Message.Chat.Id,
                 messageId: query.Message.MessageId,
                 text: result,
                 cancellationToken: ct
             );
        }
    }
}