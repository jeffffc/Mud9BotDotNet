using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;

namespace Mud9Bot.Modules.Conversations;

[Conversation("help", Description = "查看機器人指令教學")]
public class HelpConversation : IConversation
{
    public string ConversationName => "HelpFlow";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBotMetadataService _metadata;

    public HelpConversation(IServiceScopeFactory scopeFactory, IBotMetadataService metadata)
    {
        _scopeFactory = scopeFactory;
        _metadata = metadata;
    }

    public bool IsEntryPoint(Update update) 
        => update.CallbackQuery?.Data?.StartsWith("HELP+") ?? false;

    public async Task<string?> ExecuteStepAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct)
    {
        var originChatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        var callback = update.CallbackQuery;
        var userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id ?? 0;

        if (userId == 0) return null;

        if (context.CurrentState == "Start")
        {
            if (callback != null)
            {
                context.MenuMessageId = callback.Message?.MessageId ?? 0;
                context.CurrentState = "Menu";
            }
            else if (update.Message != null && update.Message.Chat.Type != ChatType.Private)
            {
                try 
                {
                    await SendHelpMenuAsync(bot, userId, context, ct, isEdit: false);
                    await bot.Reply(update.Message, "我私底下教你用啦 💁🏻", ct);
                    context.ChatId = userId; 
                    return "Menu";
                }
                catch (ApiRequestException ex) when (ex.ErrorCode == 403)
                {
                    var me = await bot.GetMe(ct);
                    var kb = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("撳呢度啟動我！ 🚀", $"http://telegram.me/{me.Username}?start=help"));
                    await bot.SendMessage(
                        chatId: originChatId,
                        text: "你未 <code>/start</code> 過我喎，快啲撳下面個制啟動咗我，再用過 <code>/help</code> 啦！",
                        parseMode: ParseMode.Html,
                        replyMarkup: kb,
                        replyParameters: new ReplyParameters { MessageId = update.Message.MessageId },
                        cancellationToken: ct
                    );
                    return null;
                }
            }
            else
            {
                return await SendHelpMenuAsync(bot, originChatId, context, ct);
            }
        }

        if (callback != null && callback.Data is { } data && data.StartsWith("HELP+"))
        {
            // 🚀 關鍵修正：不論 context 是否為新建立，均同步當前點擊的 MessageId
            // 確保 stateless 導航時 EditMessageText 能找到目標訊息
            context.MenuMessageId = callback.Message?.MessageId ?? 0;

            var parts = data.Split('+');
            string action = parts.Length > 1 ? parts[1] : "MAIN";

            if (action == "QUIT")
            {
                await bot.AnswerCallbackQuery(callback.Id, "學習完畢！", cancellationToken: ct);
                try { await bot.EditMessageText(originChatId, callback.Message!.MessageId, "教學已結束。如有需要請再次輸入 /help 🔚", cancellationToken: ct); } catch {}
                return null;
            }

            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            return await HandleHelpActionAsync(bot, originChatId, action, context, ct);
        }

        return "Menu";
    }

    private async Task<string> HandleHelpActionAsync(ITelegramBotClient bot, long chatId, string action, ConversationContext context, CancellationToken ct)
    {
        var (text, markup) = action switch
        {
            "WINE" => GetWineHelp(),
            "WEATHER" => GetWeatherHelp(),
            "TRAFFIC" => GetTrafficHelp(),
            "NEWS" => GetNewsHelp(),
            "LUCK" => GetLuckHelp(),
            "MOVIES" => GetMoviesHelp(),
            "REMIND" => await GetReminderHelp(bot, ct),
            "TOOLS" => GetToolsHelp(),
            "MISC" => GetMiscHelp(),
            "ADMIN" => GetAdminHelp(),
            "DONATE" => GetDonationHelp(),
            _ => GetMainMenu()
        };

        try
        {
            await bot.EditMessageText(
                chatId: chatId,
                messageId: context.MenuMessageId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: markup,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: ct
            );
        }
        catch (ApiRequestException) { }

        return "Menu";
    }

    private async Task<string> SendHelpMenuAsync(ITelegramBotClient bot, long chatId, ConversationContext context, CancellationToken ct, bool isEdit = false)
    {
        var (text, markup) = GetMainMenu();
        if (isEdit && context.MenuMessageId != 0)
        {
            await bot.EditMessageText(chatId, context.MenuMessageId, text, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
        else
        {
            var msg = await bot.SendMessage(chatId, text, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
            context.MenuMessageId = msg.MessageId;
        }
        return "Menu";
    }

    private (string Text, InlineKeyboardMarkup Markup) GetMainMenu()
    {
        string text = "<b>🤖 Mud9Bot 指令教學選單</b>\n\n請按以下分類查看詳細教學：\n同時請 follow @Mud9BotDev 緊貼更新！";
        var buttons = new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("賜酒派膠 🍻", "HELP+WINE"),
            InlineKeyboardButton.WithCallbackData("天氣資訊 ☁️", "HELP+WEATHER"),
            InlineKeyboardButton.WithCallbackData("交通消息 🚗", "HELP+TRAFFIC"),
            InlineKeyboardButton.WithCallbackData("新聞短打 📰", "HELP+NEWS"),
            InlineKeyboardButton.WithCallbackData("運程命理 🔮", "HELP+LUCK"),
            InlineKeyboardButton.WithCallbackData("電影資訊 🎬", "HELP+MOVIES"),
            InlineKeyboardButton.WithCallbackData("提醒功能 ⏰", "HELP+REMIND"),
            InlineKeyboardButton.WithCallbackData("實用工具 🛠️", "HELP+TOOLS"),
            InlineKeyboardButton.WithCallbackData("垃雜功能 🗑️", "HELP+MISC"),
            InlineKeyboardButton.WithCallbackData("群組管理 ⚙️", "HELP+ADMIN"),
            InlineKeyboardButton.WithCallbackData("贊助條款 💖", "HELP+DONATE")
        };

        var rows = buttons.Chunk(2).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("完成 ✔️", "HELP+QUIT") });
        return (text, new InlineKeyboardMarkup(rows));
    }

    private (string, InlineKeyboardMarkup) GetWineHelp()
    {
        string text = "<b>【賜酒派膠】</b>\n以下指令只限於群組內使用：\n\n" +
                      "• <code>/z</code> (回覆訊息): 對目標用戶進行賜酒或派膠\n" +
                      "• <code>/z 10</code> (回覆訊息): 一次過賜/派多個數量 (上限 20)\n" +
                      "• <code>/check</code>: 查詢自己喺該群組獲得及剩餘嘅配額";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetWeatherHelp()
    {
        string text = "<b>【天氣資訊】</b>\n\n" +
                      "• <code>/weather</code>: 查看本港現時氣溫、濕度及各分區溫度\n" +
                      "• <code>/forecast</code>: 查看本港未來九天天氣預報\n" +
                      "• <b>快捷關鍵字：</b>直接輸入「<code>而家天氣</code>」可獲取現時概況";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetTrafficHelp()
    {
        string text = "<b>【交通資訊】</b>\n\n" +
                      "• <code>/traffic</code>: 獲取 RTHK 即時交通消息 (文字版)\n" +
                      "• <code>/snapshot</code>: 查看本港各區交通快拍路面情況 (只限私訊)\n" +
                      "• <b>快捷關鍵字：</b>直接輸入「<code>交通消息</code>」快速查看即時簡報";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetNewsHelp()
    {
        string text = "<b>【新聞短打】</b>\n\n" +
                      "• <code>/news</code>: 開啟新聞分類選單\n" +
                      "• <b>快捷關鍵字：</b>直接輸入「<code>有咩新聞</code>」可觸發\n\n" +
                      "<b>包含分類：</b>\n" +
                      "本地、大中華、國際、財經、體育新聞 (每類顯示 5 則最新資訊)。";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetLuckHelp()
    {
        string text = "<b>【運程命理】</b>\n\n" +
                      "• <code>/fortune</code>: 黃大仙靈籤 (每日限求一籤，可解籤)\n" +
                      "• <code>/zodiac</code>: 每日星座運程 (整體/愛情/事業/財運)\n" +
                      "• <code>/mark6</code>: 最新一期六合彩開獎結果 (亦可輸入「<code>六合彩結果</code>」)";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetMoviesHelp()
    {
        string text = "<b>【電影資訊】</b>\n\n" +
                      "• <code>/movies</code>: 查看現在上映電影資訊及簡介\n" +
                      "• <b>快捷關鍵字：</b>直接輸入「<code>有咩戲睇</code>」可觸發\n\n" +
                      "系統會自動更新本港各大院線熱映中嘅電影評價及詳情。";
        return (text, GetBackMarkup());
    }

    private async Task<(string, InlineKeyboardMarkup)> GetReminderHelp(ITelegramBotClient bot, CancellationToken ct)
    {
        var me = await bot.GetMe(ct);
        var sb = new StringBuilder();
        sb.AppendLine("<b>【⏰ 廣東話提醒功能指南】</b>");
        sb.AppendLine("你可以直接用廣東話叫我提你做嘢，格式非常彈性：\n");
        
        sb.AppendLine("<b>1️⃣ 相對時間 (倒數)</b>");
        sb.AppendLine("• <code>10分鐘後提我落街</code>");
        sb.AppendLine("• <code>2個鐘後提我食藥</code>");
        sb.AppendLine("• <code>5個月後提我續約</code>");
        sb.AppendLine("• <code>1年後提我換車</code>\n");

        sb.AppendLine("<b>2️⃣ 指定日期 / 星期</b>");
        sb.AppendLine("• <b>今日/聽日/後日：</b><code>今日 16:30 提我買奶茶</code>");
        sb.AppendLine("<i>(註：「今日」為可選項目，例如直接講「16:30 提我」亦可)</i>");
        sb.AppendLine("• <b>星期：</b><code>星期一 10點 提我開會</code> / <code>下星期五 提我攞衫</code>");
        sb.AppendLine("• <b>具體日期：</b>支援 8 位數字 YYYYMMDD (如 <code>20260210 提我</code>)");
        sb.AppendLine("• <b>支援分隔符：</b>日期可加入 <code>/</code>, <code>-</code>, <code>.</code> (如 <code>2026/02/10 提我</code>)\n");

        sb.AppendLine("<b>3️⃣ 時間格式與區分規則</b>");
        sb.AppendLine("• <b>4 位純數字：</b>永遠視為時間 (HHmm)。例如 <code>0210 提我</code> 即係凌晨 02:10。");
        sb.AppendLine("• <b>8 位純數字：</b>永遠視為日期 (YYYYMMDD)。");
        sb.AppendLine("• <b>過期處理：</b>若指定時間已經過咗，我會幫你設為<b>聽日</b>。");
        sb.AppendLine("• <b>缺省時間：</b>若冇講幾點 (如「聽日提我」)，我會喺<b>聽日嘅宜家呢個時間</b>搵你。\n");

        sb.AppendLine("<b>4️⃣ 重複性提醒 🔄</b>");
        sb.AppendLine("• <b>每日：</b><code>每日 08:00 提我食藥</code> / <code>逢日 23:00 填寫日誌</code>");
        sb.AppendLine("• <b>每週：</b><code>逢星期二 18:00 提我打波</code> / <code>每星期五 提我執屋</code>\n");

        sb.AppendLine("<b>5️⃣ 管理及限制</b>");
        sb.AppendLine("• 輸入 <code>/myreminders</code> 查看或刪除生效中嘅提醒。");
        sb.AppendLine("• 為免資源浪費，每人上限為 <b>30 條</b> 提醒事項。");
        sb.AppendLine("• 系統最高支援排程至公元 <b>9999年12月31日</b>。");

        var buttons = new List<IEnumerable<InlineKeyboardButton>>
        {
            new[] { InlineKeyboardButton.WithUrl("⚙️ 立即管理我嘅提醒", $"https://t.me/{me.Username}?start=myreminders") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 返回主目錄", "HELP+MAIN") }
        };
        return (sb.ToString(), new InlineKeyboardMarkup(buttons));
    }

    private (string, InlineKeyboardMarkup) GetToolsHelp()
    {
        string text = "<b>【實用工具】</b>\n\n" +
                      "• <code>/ch 字</code>: 查詢中文字倉頡碼 (一次最多 20 字)\n" +
                      "• <code>/t 內容</code>: 翻譯文字 (支援回覆訊息、直接輸入或英漢自動偵測)\n" +
                      "• <code>/speech</code>: (回覆語音) 語音轉文字辨識功能\n" +
                      "• <code>/gas</code>: 查詢本港各大油站即時油價 (無鉛、特級、柴油)\n" +
                      "• <b>匯率轉換：</b>直接輸入 <code>100 usd to hkd</code> 即時查詢國際即時匯率";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetMiscHelp()
    {
        string text = "<b>【垃雜功能】</b>\n\n" +
                      "• <code>/toss A B C</code>: 擲銀仔或從多個選項中隨機抽取\n" +
                      "• <code>/dice NdS</code>: TRPG 擲骰格式 (N=粒數, S=面數)\n" +
                      "  - <code>/dice 1d6</code> (擲 1 粒 6 面骰)\n" +
                      "  - <code>/dice 2d20</code> (擲 2 粒 20 面骰)\n" +
                      "  - <code>/dice 3d10 5</code> (擲 3 粒 10 面骰，重複 5 次)\n" +
                      "• <code>/block</code> (回覆訊息): 顯示已封鎖用戶 (純屬娛樂功能)\n" +
                      "• <code>/ping</code>: 檢查機器人連線狀態\n" +
                      "• <code>/info</code>: 顯示機器人資訊與更新頻道\n" +
                      "• <code>/feedback 內容</code>: 向開發者提交意見或回報問題\n\n" +
                      "<b>🛡️ 被動攔截 (群組設定)：</b>\n" +
                      "• <b>5P字過濾：</b>自動警告使用 5P 字體之用戶\n" +
                      "• <b>殘體字攔截：</b>自動警告使用簡體中文之用戶";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetAdminHelp()
    {
        string text = "<b>【群組管理】</b>\n(群組管理員專用指令)\n\n" +
                      "• <code>/msettings</code>: 開啟群組設定選單\n\n" +
                      "<b>⚙️ 選單可控制內容包括：</b>\n" +
                      "• <b>酒膠配額：</b>設定各成員每日可賜出之酒/膠總數\n" +
                      "• <b>功能開關：</b>個別啟用或禁用求籤、星座功能\n" +
                      "• <b>內容過濾：</b>開啟或關閉「5P字過濾」及「殘體字攔截」系統\n\n" +
                      "<b>👋 設定歡迎訊息：</b>\n" +
                      "• <code>/welcome [內容]</code>: 設定群組嘅歡迎訊息。每當有用戶加入或被邀請入群時，就會發送呢條訊息。如果內容輸入 <code>no</code> 或 <code>-</code>，則會停用歡迎訊息。\n" +
                      "<i>💡 你亦可以對住一則包含相片或 GIF 嘅訊息回覆 /welcome，將其設為附帶媒體嘅歡迎訊息。</i>\n\n" +
                      "<b>歡迎訊息內可以使用以下變數：</b>\n" +
                      "<code>$name</code> - 新成員名稱\n" +
                      "<code>$username</code> - 新成員 Username\n" +
                      "<code>$id</code> - 新成員 ID\n" +
                      "<code>$title</code> - 群組名稱\n" +
                      "<code>$language</code> - 語言代碼\n\n" +
                      "<b>🎨 支援 HTML 格式排版：</b>\n" +
                      "• 粗體：<code>&lt;b&gt;文字&lt;/b&gt;</code>\n" +
                      "• 斜體：<code>&lt;i&gt;文字&lt;/i&gt;</code>\n" +
                      "• 底線：<code>&lt;u&gt;文字&lt;/u&gt;</code>\n" +
                      "• 刪除線：<code>&lt;s&gt;文字&lt;/s&gt;</code>\n" +
                      "• 劇透/防雷：<code>&lt;tg-spoiler&gt;文字&lt;/tg-spoiler&gt;</code>\n" +
                      "• 超連結：<code>&lt;a href=\"網址\"&gt;文字&lt;/a&gt;</code>\n" +
                      "• 單行代碼：<code>&lt;code&gt;文字&lt;/code&gt;</code>\n" +
                      "• 多行代碼塊：<code>&lt;pre&gt;文字&lt;/pre&gt;</code>\n" +
                      "• 引用區塊：<code>&lt;blockquote&gt;文字&lt;/blockquote&gt;</code>\n\n" +
                      "<b>📝 例子：</b>\n" +
                      "<code>/welcome 歡迎 &lt;b&gt;$name&lt;/b&gt; 來到 &lt;i&gt;$title&lt;/i&gt;！請先閱讀 &lt;a href=\"https://t.me/telegram\"&gt;群規&lt;/a&gt;。</code>";
        return (text, GetBackMarkup());
    }

    private (string, InlineKeyboardMarkup) GetDonationHelp()
    {
        string text = "<b>【贊助及條款】</b>\n\n" +
                      "• <code>/donate [Stars amount]</code>: 使用 Telegram Stars 支持機器人運作 (例如 <code>/donate 50</code>)\n" +
                      "• <code>/terms</code>: 查看贊助相關詳細條款及細則 (T&C)\n\n" +
                      "🙇🏻‍♂️ 多謝你支持 Mud9Bot 嘅開發同伺服器支出！🙇🏻‍♂️";
        return (text, GetBackMarkup());
    }

    private InlineKeyboardMarkup GetBackMarkup() 
        => new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("🔙 返回主目錄", "HELP+MAIN"));
}