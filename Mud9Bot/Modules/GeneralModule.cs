using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class GeneralModule(DonationModule donationModule, IConfiguration configuration)
{
    [Command("start", Description = "Start the bot")]
    public async Task Start(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        // 處理 Deep Linking: 如果參數是 "donate"，跳轉到 DonationModule 的處理邏輯
        if (args.Length > 0 && args[0].Equals("donate", StringComparison.OrdinalIgnoreCase))
        {
            // 調用 DonationModule 的指令方法，傳入空的 args (因為已進入私訊)
            await donationModule.DonateCommand(bot, msg, Array.Empty<string>(), ct);
            return;
        }

        // 標準的 /start 回覆
        await bot.Reply(msg, "Hello! I am Mud9Bot using Attributes!", ct);
    }

    [Command("ping")]
    public async Task Ping(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        await bot.Reply(msg, "Pong!", ct);
    }
    
    [Command("toss", Description = "擲銀仔或隨機抽籤")]
    public async Task TossCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            // 擲銀仔：50% 機率
            string result = Random.Shared.Next(0, 100) < 50 ? "公！" : "字！";
            await bot.Reply(message, result, ct);
        }
        else if (args.Length == 1)
        {
            // 只有一個選項
            await bot.Reply(message, "得一樣嘢仲要我幫你揀咩？", ct);
        }
        else
        {
            // 隨機抽籤
            int index = Random.Shared.Next(0, args.Length);
            
            // 安全處理：由於 Reply 擴充方法預設使用 HTML，需對抽中的文字進行 Encode
            string chosen = args[index].EscapeHtml();
            
            await bot.Reply(message, $"{chosen}!", ct);
        }
    }
    
    [Command("feedback", Description = "提供意見回饋")]
    public async Task FeedbackCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            await bot.Reply(message, "你想提供咩意見呀？請喺指令後面加上內容，例如：<code>/feedback 呢個功能好正！</code>", ct: ct);
            return;
        }

        var feedbackText = string.Join(" ", args).EscapeHtml();
        var user = message.From;
        
        var logGroupId = configuration.GetValue<long>("BotConfiguration:LogGroupId");

        if (logGroupId != 0)
        {
            // 將用戶名改為可點擊連結，導向用戶 Profile
            string adminLog = $"📝 <b>收到新意見回饋！</b>\n" +
                              $"👤 <b>用戶：</b> <a href=\"tg://user?id={user?.Id}\">{user?.FirstName.EscapeHtml()}</a> (<code>{user?.Id}</code>)\n" +
                              $"💬 <b>內容：</b>\n{feedbackText}";

            await bot.SendMessage(logGroupId, adminLog, parseMode: ParseMode.Html, cancellationToken: ct);
            await bot.Reply(message, "多謝你嘅意見！我已經轉告咗畀開發者聽喇。💖", ct: ct);
        }
    }
}