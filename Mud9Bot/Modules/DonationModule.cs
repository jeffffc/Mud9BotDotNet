using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Mud9Bot.Modules;

public class DonationModule
{
    [Command("donate", "star", Description = "支持開發者 (使用 Telegram Stars)")]
    public async Task DonateCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. 檢查是否為私訊，若在群組則引導至私訊
        if (message.Chat.Type != ChatType.Private)
        {
            var me = await bot.GetMe(ct);
            var keyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl("按此私訊支持 💖", $"https://t.me/{me.Username}?start=donate")
            );

            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: "呢度用唔到，要私訊先得。🔒\n請撳下面個制去私訊搵我啦！",
                replyMarkup: keyboard,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: ct);
            return;
        }

        // 2. 檢查是否有輸入金額
        if (args.Length < 1)
        {
            string msg = "想捐幾錢俾我？❤️\n用 <code>/donate 50</code> 自己改金額啦!";
            await bot.SendMessage(message.Chat.Id, msg, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        // 3. 檢查金額是否為整數
        if (!int.TryParse(args[0], out int money))
        {
            await bot.Reply(message, "捐款只能為整數，我地唔收毫子。", ct: ct);
            return;
        }

        // 4. 檢查最低捐款額 (至少 50 Stars)
        if (money < 50)
        {
            await bot.Reply(message, "唔好意思，捐款下限係 50 Telegram Stars。", ct: ct);
            return;
        }

        // 5. 發送捐贈前置資訊
        string infoMsg = "多謝你捐款俾 @Mud9Bot 啊！感激不盡！\n" +
                         "💡 <b>溫馨提示：</b>100 Stars 大約等於 2 USD / 16 HKD。\n" +
                         "如果你唔夠 Stars，可以去 <code>Settings > Stars (設定 > 我的星星)</code> 買咗先。\n\n" +
                         "請檢查下下面啲資料啱唔啱，睇清楚先好撳制俾錢！\n" +
                         "詳細 T&C 請按 /terms，有問題亦可搵 @Mud9BotSupport ，或者用 <code>/feedback &lt;內容&gt;</code>";

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: infoMsg,
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        // 6. 發送 Invoice
        var prices = new[]
        {
            new LabeledPrice("是次捐款", money)
        };

        string title = $"贊助 Mud9Bot - {money} ⭐";
        string description = $"贊助 Mud9Bot {money} Telegram Stars";
        string payload = $"mud9botdonation:{message.From?.Id}";

        try
        {
            await bot.SendInvoice(
                chatId: message.Chat.Id,
                title: title,
                description: description,
                payload: payload,
                providerToken: "", 
                currency: "XTR",   
                prices: prices,
                startParameter: "donate",
                cancellationToken: ct
            );
        }
        catch (Exception)
        {
            await bot.Reply(message, "好似有啲問題，捐款失敗。不過向你保證今次捐款一定唔會扣數，或者你介唔介意試多一次？", ct: ct);
        }
    }

    [Command("terms", Description = "查看贊助條款及細則")]
    public async Task TermsCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (message.Chat.Type != ChatType.Private) return;

        string terms = "<b>【@Mud9Bot 贊助詳細條款（T&C）】</b>\n\n" +
                       "💡 <b>乜嘢係 Telegram Stars？</b>\n" +
                       "簡單嚟講，Stars 係 Telegram 官方推出嘅虛擬代幣，等你可以直接喺 App 入面支持鍾意嘅 Creator 同 Bot。如果你想了解更多技術細節或者官方介紹，可以睇呢度：<a href=\"https://telegram.org/blog/telegram-stars\">Telegram Stars Blog</a>\n\n" +
                       "1. 為保障帳戶及交易安全，強烈建議用家開啟 <a href=\"https://telegram.org/faq#q-how-does-2-step-verification-work\">Two-Step Verification</a>。\n\n" +
                       "2. 本服務使用 Telegram Stars 進行交易，相關操作受 <a href=\"https://telegram.org/tos/stars\">Telegram Stars Terms of Service</a> 約束。\n\n" +
                       "3. Telegram 只作中介，任何付款相關問題請聯絡 @Mud9BotSupport。\n\n" +
                       "4. Telegram Bot Support (@BotSupport) 將不會為是次交易提供任何協助。\n\n" +
                       "5. 贊助均為自願性質，一經確認恕不退還。多謝你支持 Mud9Bot 嘅運行！";

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: terms,
            parseMode: ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct);
    }
}