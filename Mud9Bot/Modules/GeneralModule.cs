using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class GeneralModule(
    DonationModule donationModule, 
    IConfiguration configuration, 
    ILomoService lomoService, 
    IGroupService groupService,
    ISimplifiedChineseService simplifiedService)
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
    
    [Command("dice", Description = "擲骰仔 (格式: /dice [數量]d[面數] [重複次數])")]
    public async Task DiceCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. 如果沒有任何參數，預設擲一顆六面骰，並提供友善提示
        if (args.Length == 0)
        {
            int defaultRoll = Random.Shared.Next(1, 7);
            string defaultMsg = $"🎲 幫你擲咗 1 粒 6 面骰，結果係：<b>{defaultRoll}</b>\n💡 <i>提示：你可以用 <code>/dice 2d20 3</code> 嚟自訂數量同面數㗎！</i>";
            await bot.Reply(message, defaultMsg, ct: ct);
            return;
        }

        // 2. 解析正則表達式 (例如 1d6, 2d10, D20)
        var match = Regex.Match(args[0], @"^(\d*)[dD](\d+)$");
        if (match.Success)
        {
            // 如果開頭沒有數字 (例如 d6)，預設為 1
            int count = string.IsNullOrEmpty(match.Groups[1].Value) ? 1 : int.Parse(match.Groups[1].Value);
            int sides = int.Parse(match.Groups[2].Value);
            int repeats = 1;

            if (args.Length > 1 && int.TryParse(args[1], out int r))
            {
                repeats = r;
            }

            // 防呆機制：嚴格限制以保持訊息短小精悍
            count = Math.Clamp(count, 1, 10);
            sides = Math.Clamp(sides, 2, 100);
            repeats = Math.Clamp(repeats, 1, 10);

            var sb = new StringBuilder();
            sb.AppendLine($"🎲 <b>擲骰結果 ({count}d{sides})</b>");
            
            if (repeats > 1) 
            {
                sb.AppendLine($"<i>重複 {repeats} 次：</i>\n");
            }
            
            for (int i = 0; i < repeats; i++)
            {
                var rolls = new List<int>();
                for (int j = 0; j < count; j++)
                {
                    rolls.Add(Random.Shared.Next(1, sides + 1));
                }

                // 美化輸出邏輯
                string prefix = repeats > 1 ? $"{i + 1}. " : "";
                
                if (count == 1)
                {
                    sb.AppendLine($"{prefix}結果：<b>{rolls[0]}</b>");
                }
                else
                {
                    sb.AppendLine($"{prefix}[ {string.Join(", ", rolls)} ] ➔ 總和：<b>{rolls.Sum()}</b>");
                }
            }

            await bot.Reply(message, sb.ToString().TrimEnd(), ct: ct);
        }
        else
        {
            // 3. 格式錯誤時的提示訊息，同步更新範例指令
            string helpMsg = "⚠️ <b>骰仔格式錯咗呀！</b>\n\n" +
                             "請使用標準 TRPG 擲骰格式 <code>NdS</code>：\n" +
                             "• <code>N</code> = 骰仔數量\n" +
                             "• <code>S</code> = 骰仔面數\n\n" +
                             "💡 <b>例子：</b>\n" +
                             "• <code>/dice 1d6</code> (擲 1 粒 6 面骰)\n" +
                             "• <code>/dice 2d20</code> (擲 2 粒 20 面骰)\n" +
                             "• <code>/dice 3d10 5</code> (擲 3 粒 10 面骰，重複 5 次)";
                             
            await bot.Reply(message, helpMsg, ct: ct);
        }
    }
    
    // ---------------------------------------------------------
    // MK Word Filter (5P 驅逐器)
    // ---------------------------------------------------------
    [TextTrigger("[吾系甘牙禾巧挽莪尒芣]", Description = "MK Word Filter (Lomo Filter)")]
    public async Task MkWordFilterAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.Text == null) return;
        long chatId = message.Chat.Id;

        // 1. 檢查群組設定
        if (message.Chat.Type != ChatType.Private)
        {
            var group = await groupService.GetGroupSettingsAsync(chatId, ct);
            if (group != null && group.OffLomo) return;
        }

        string text = message.Text;

        // 2. 從 Service 獲取動態排除字詞清單
        var ignoreWords = lomoService.GetIgnoreWords();
        
        // 按照字數由長到短排序替換，避免例如「甘拜下風」被「甘」先切斷
        var sortedIgnore = ignoreWords.OrderByDescending(w => w.Length);

        foreach (var ignoreKey in sortedIgnore)
        {
            if (text.Contains(ignoreKey))
            {
                text = text.Replace(ignoreKey, "");
            }
        }

        // 3. 檢查剩下的文字是否包含 MK 字
        string mkCharacters = "吾系甘牙禾巧挽莪尒芣";
        var caughtChars = new StringBuilder();

        foreach (char c in mkCharacters)
        {
            if (text.Contains(c))
            {
                caughtChars.Append(c);
            }
        }

        // 4. 如果有抓到，則進行回覆
        if (caughtChars.Length > 0)
        {
            string reply = $"{caughtChars}你老母？！";
            await bot.Reply(message, reply, ct: ct);
        }
    }

    // ---------------------------------------------------------
    // Lomo Ignore List 管理 (DevOnly)
    // ---------------------------------------------------------

    [Command("addignore", DevOnly = true, Description = "新增 Lomo 過濾排除字詞")]
    public async Task AddIgnoreCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            await bot.Reply(message, "你要加邊個排除字？用法：<code>/addignore 老掉牙</code>", ct: ct);
            return;
        }

        string word = args[0];
        await lomoService.AddWordAsync(word);
        await bot.Reply(message, $"✅ 已將「{word}」加入排除清單。", ct: ct);
    }

    [Command("delignore", DevOnly = true, Description = "刪除 Lomo 過濾排除字詞")]
    public async Task DelIgnoreCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            await bot.Reply(message, "你要刪邊個排除字？用法：<code>/delignore 老掉牙</code>", ct: ct);
            return;
        }

        string word = args[0];
        bool removed = await lomoService.RemoveWordAsync(word);
        
        string reply = removed ? $"🗑 已從清單移除「{word}」。" : "🔍 搵唔到呢個字喎。";
        await bot.Reply(message, reply, ct: ct);
    }
    
    // ---------------------------------------------------------
    // Simplified Chinese Filter (殘體字攔截)
    // ---------------------------------------------------------
    [TextTrigger(@"\p{IsCJKUnifiedIdeographs}", Description = "Simplified Chinese Filter")]
    public async Task SimplifiedChineseDetectionAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.Text == null) return;
        long chatId = message.Chat.Id;

        // 1. 檢查群組設定 (使用 IGroupService 快取)
        if (message.Chat.Type != ChatType.Private)
        {
            var group = await groupService.GetGroupSettingsAsync(chatId, ct);
            if (group == null || group.OffSimp) return;
        }

        // 2. 使用新註冊的 Service 進行識別
        // IsSimplified 會在結果為 Simplified 或 Both 時回傳 true
        if (simplifiedService.IsSimplified(message.Text))
        {
            await bot.Reply(message, "用乜撚嘢殘體啊，當呢度大陸啊？", ct: ct);
        }
    }
}