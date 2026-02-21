using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class BlockModule(IUserService userService)
{
    [Command("block")]
    public async Task BlockCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 此指令限制在群組中使用
        if (message.Chat.Type == ChatType.Private)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id, 
                text: "呢度用唔到，要群組先得。", 
                cancellationToken: ct);
            return;
        }

        // 必須對目標訊息進行回覆
        if (message.ReplyToMessage == null)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id, 
                text: "你想 block 邊個呀？對住佢 <code>/block</code> 啦！", 
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        var sender = message.From;
        var target = message.ReplyToMessage.From;

        // 同步發送者與被標記者的資料到資料庫
        if (sender != null) await userService.SyncUserAsync(sender, ct);
        if (target != null) await userService.SyncUserAsync(target, ct);

        string replyToName = target?.FirstName ?? "Unknown";
        if (!string.IsNullOrWhiteSpace(target?.LastName))
        {
            replyToName += " " + target.LastName;
        }

        // 轉用 HTML 轉義以配合 ParseMode.Html
        string safeName = replyToName.EscapeHtml();
        string msg = $"( Show blocked user - {safeName} )";

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: msg,
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.ReplyToMessage.MessageId },
            cancellationToken: ct
        );
    }
}