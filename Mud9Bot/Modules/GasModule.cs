using System.Text;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class GasModule(IGasService gasService)
{
    [Command("gas", "oil", Description = "查詢本港各大油站即時油價 (來源: 消委會)")]
    public async Task GasCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var data = gasService.GetCachedPrices();

        if (!data.Any())
        {
            await bot.Reply(message, "暫時未有油價資料，等我更新下先。⛽", ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("<b>⛽ 本港即時牌價參考 (每升)</b>");
        sb.AppendLine($"<code>(更新時間：{gasService.LastUpdated.ToHkTime():yyyy-MM-dd HH:mm})</code>\n");

        foreach (var item in data)
        {
            sb.AppendLine($"🔹 <b>{item.Type.Tc}</b>");
            
            // 按價格排序，讓用戶一眼看到最平的是哪間
            var sortedPrices = item.Prices
                .Select(p => new { Vendor = p.Vendor.Tc, Price = p.Price.Trim() })
                .OrderBy(p => p.Price)
                .ToList();

            foreach (var p in sortedPrices)
            {
                sb.AppendLine($"├ {p.Vendor.PadRight(5)}：<code>${p.Price}</code>");
            }
            sb.AppendLine();
        }

        sb.AppendLine("<i>* 註：以上為官方牌價，未計及個別信用卡或油卡優惠。資料來源：消委會</i>");

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
}