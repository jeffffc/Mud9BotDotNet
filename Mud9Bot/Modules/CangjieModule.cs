using System.Text;
using System.Text.RegularExpressions;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Mud9Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class CangjieModule(ICangjieService cjService)
{
    private static readonly Regex ChineseRegex = new(@"\p{IsCJKUnifiedIdeographs}");

    // 在描述中加入限制提示
    [Command("ch", Description = "查詢中文字倉頡碼 (每次最多 20 字)")]
    public async Task CangjieCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            // 在用法提示中加入字數限制說明
            await bot.Reply(message, "你要查邊個字？用法：<code>/ch 倉頡</code> (每次最多查 20 個字)", ct: ct);
            return;
        }

        string input = string.Join("", args);
        
        // 使用 Service 中定義的常數進行判斷
        if (input.Length > CangjieService.MaxInputLength)
        {
            await bot.Reply(message, $"唔好心急，一次最多查 {CangjieService.MaxInputLength} 個字呀。⚖️", ct: ct);
            return;
        }

        var sb = new StringBuilder();
        bool found = false;

        sb.AppendLine("<pre>");
        sb.AppendLine("字 | 碼    | 倉頡根");
        sb.AppendLine("---|-------|-------");

        foreach (char c in input)
        {
            if (!ChineseRegex.IsMatch(c.ToString())) continue;

            var result = cjService.GetCode(c);
            if (result.HasValue)
            {
                found = true;
                sb.AppendLine($"{c.ToString().PadRight(2)} | {result.Value.Code.PadRight(5)} | {result.Value.Radicals}");
            }
            else
            {
                sb.AppendLine($"{c.ToString().PadRight(2)} | 未獲取 | -");
            }
        }
        sb.AppendLine("</pre>");

        if (!found)
        {
            await bot.Reply(message, "查唔倒呢啲字。🧐", ct: ct);
            return;
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: $"<b>🔍 倉頡查碼結果：</b>\n{sb}",
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
}