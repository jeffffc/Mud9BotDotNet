using System.Text;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class MarkSixModule(IMarkSixService markSixService)
{
    [Command("mark6", "marksix", Description = "查看最近一期六合彩開獎結果")]
    [TextTrigger("六合彩結果", Description = "查詢六合彩開獎結果")]
    public async Task HandleMarkSixAsync(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var result = markSixService.GetLatestResult();

        if (result == null)
        {
            await bot.Reply(message, "暫時未有六合彩資料，等我收下風先。🎰", ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("<b>🎰 六合彩最新開獎結果</b>");
        sb.AppendLine($"<code>{result.Period.EscapeHtml()}</code>");
        sb.AppendLine();
        
        string balls = string.Join(" , ", result.Numbers.Select(n => $"<b>{n}</b>"));
        sb.AppendLine($"正碼：{balls}");
        sb.AppendLine($"特別號碼：<b>{result.SpecialBall}</b> 🔴");
        sb.AppendLine();

        if (result.Prizes.Any())
        {
            sb.AppendLine("<b>【派彩詳情】</b>");
            foreach (var p in result.Prizes)
            {
                sb.AppendLine($"• {p.EscapeHtml()}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(result.NextDrawTime))
        {
            sb.AppendLine("<b>【下期資訊】</b>");
            sb.AppendLine($"⏳ 截止售票：<code>{result.NextDrawTime.EscapeHtml()}</code>");
            if (!string.IsNullOrEmpty(result.NextJackpot))
            {
                sb.AppendLine($"💰 估計彩金：<b>{result.NextJackpot.EscapeHtml()}</b>");
            }
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
}