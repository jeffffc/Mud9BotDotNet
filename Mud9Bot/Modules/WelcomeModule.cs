using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Mud9Bot.Modules;

public class WelcomeModule(IServiceScopeFactory scopeFactory)
{
    [Command("welcome", Description = "設定群組入群歡迎訊息", AdminOnly = true, GroupOnly = true)]
    public async Task WelcomeCommand(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        var text = string.Join(" ", args);
        var reply = msg.ReplyToMessage;

        // 如果沒有在指令後面加文字，嘗試擷取回覆訊息中的文字或相片/GIF說明
        if (string.IsNullOrWhiteSpace(text) && reply != null)
        {
            text = reply.Text ?? reply.Caption ?? "";
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == msg.Chat.Id, ct);
        
        if (group == null) return;

        // 清除歡迎訊息機制 ("no" 或 "-")
        if (text.Equals("no", StringComparison.OrdinalIgnoreCase) || text == "-")
        {
            group.WelcomeText = null;
            group.WelcomePhoto = null;
            group.WelcomeGif = null;
            
            await db.SaveChangesAsync(ct);
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            groupService.RefreshCache(group);
            
            await bot.Reply(msg, "✅ 已停用歡迎訊息。", ct: ct);
            return;
        }

        // 如果還是沒有文字，顯示教學
        if (string.IsNullOrWhiteSpace(text))
        {
            string helpMsg = "<b>【設定歡迎訊息】</b>\n" +
                             "請輸入歡迎訊息內容，或對住一則包含文字/相片/GIF 嘅訊息回覆 <code>/welcome</code>。\n\n" +
                             "<b>可用變數：</b>\n" +
                             "<code>$name</code> - 新成員名稱\n" +
                             "<code>$username</code> - 新成員 Username\n" +
                             "<code>$id</code> - 新成員 ID\n" +
                             "<code>$title</code> - 群組名稱\n" +
                             "<code>$language</code> - 語言代碼\n\n" +
                             "<b>停用方法：</b> <code>/welcome no</code> 或 <code>/welcome -</code>";
                             
            await bot.SendMessage(msg.Chat.Id, helpMsg, parseMode: ParseMode.Html, replyParameters: new ReplyParameters{ MessageId = msg.MessageId }, cancellationToken: ct);
            return;
        }

        // 擷取媒體 ID (若回覆的對象包含圖片或 GIF 動畫)
        string? photoId = reply?.Photo?.LastOrDefault()?.FileId;
        string? gifId = reply?.Animation?.FileId ?? (reply?.Document?.MimeType == "video/mp4" ? reply.Document.FileId : null);

        group.WelcomeText = text;
        group.WelcomePhoto = photoId;
        group.WelcomeGif = gifId;

        await db.SaveChangesAsync(ct);
        var groupService2 = scope.ServiceProvider.GetRequiredService<IGroupService>();
        groupService2.RefreshCache(group);

        string mediaType = photoId != null ? " (連同相片 📸)" : gifId != null ? " (連同 GIF 🎞️)" : "";
        await bot.Reply(msg, $"✅ 歡迎訊息設定成功{mediaType}！當新成員加入時將會觸發。", ct: ct);
    }
}