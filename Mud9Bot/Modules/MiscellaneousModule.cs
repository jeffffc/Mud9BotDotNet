using System.Runtime.InteropServices.ComTypes;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class MiscellaneousModule(IServiceScopeFactory scopeFactory, IUserService userService)
{
    [Command("block")]
    public async Task BlockCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. Private chat check
        if (message.Chat.Type == ChatType.Private)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id, 
                text: "呢度用唔到，要群組先得。", 
                cancellationToken: ct);
            return;
        }

        // 2. Require Reply-To message
        if (message.ReplyToMessage == null)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id, 
                text: "你想 block 邊個呀？對住佢 `/block` 啦！", 
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            return;
        }

        var sender = message.From;
        var target = message.ReplyToMessage.From;

        // 3. Sync users to Database (Optional but recommended for consistency)
        if (sender != null) await userService.SyncUserAsync(sender, ct);
        if (target != null) await userService.SyncUserAsync(target, ct);

        // 4. Extract Target Name
        string replyToName = target?.FirstName ?? "Unknown";
        if (!string.IsNullOrWhiteSpace(target?.LastName))
        {
            replyToName += " " + target.LastName;
        }

        // 5. Send Block Message
        string msg = $"( Show blocked user - {replyToName.EscapeHtml()} )";

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: msg,
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.ReplyToMessage.MessageId },
            cancellationToken: ct
        );
    }

    [Command("never")]
    public async Task NeverCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
#if DEBUG
        string neverVoiceId = "AwACAgUAAx0CPwaOqQACFc5pl0Q7UaIuW7ychwnzBbtpIMCfIgACBwAD7aMgVtfzCftUHidsOgQ";
#elif RELEASE
        string neverVoiceId = "AwADBQADBwAD7aMgVuu1vVWYwaY3Ag";
#endif
        await bot.SendVoice(
            chatId: message.Chat.Id,
            voice: neverVoiceId,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
            );
    }
    
    [Command("x")]
    public async Task RickRollCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
#if DEBUG
        string rickRollVoiceId = "AwACAgQAAx0CPwaOqQACFc9pl0REMNTtqYlMN1lCiBu2pSPVrQACvDkAAoMdZAdMEEIMsaeDQzoE";
#elif RELEASE
        string rickRollVoiceId = "AwADBAADvDkAAoMdZAdKyR43rFpUjgI";
#endif
        await bot.SendVoice(
            chatId: message.Chat.Id,
            voice: rickRollVoiceId,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
}