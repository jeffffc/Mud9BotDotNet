using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;

namespace Mud9Bot.Modules.Conversations;

[Conversation("msettings", Description = "Configure group settings")]
public class SettingsConversation : IConversation
{
    public string ConversationName => "SettingsFlow";
    
    // Updated delimiter check to +
    public bool IsEntryPoint(Update update) 
        => update.CallbackQuery?.Data?.StartsWith("SETTINGS+") ?? false;
    
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsConversation(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    private class SettingsSession
    {
        public int MessageId { get; set; }
        public long TargetGroupId { get; set; }
    }

    public async Task<string?> ExecuteStepAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct)
    {
        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        var callback = update.CallbackQuery;

        // Updated delimiter logic (Fixes Restart Issue)
        if (context.CurrentState == States.StateStart && callback != null && callback.Data is { } cbData && cbData.StartsWith("SETTINGS+"))
        {
            var parts = cbData.Split('+');
            // Format: SETTINGS+{TargetGroupId}+{Type}+{Action}
            if (parts.Length >= 2 && long.TryParse(parts[1], out long savedGroupId))
            {
                int msgId = callback.Message?.MessageId ?? 0;
                context.MenuMessageId = msgId; // <--- 補上這行
                context.Data["Session"] = new SettingsSession 
                { 
                    MessageId = msgId,
                    TargetGroupId = savedGroupId 
                };
                context.CurrentState = States.StateMenu; 
            }
        }

        // 1. ENTRY POINT
        if (context.CurrentState == States.StateStart)
        {
            return await SendMainMenuAsync(bot, update, context, ct);
        }

        // 2. GLOBAL MENU HANDLER
        if (callback != null && callback.Data is { } data && data.StartsWith("SETTINGS+"))
        {
            // A. Validate Menu Freshness
            if (context.Data.TryGetValue("Session", out var sessionObj) && sessionObj is SettingsSession session)
            {
                if (callback.Message?.MessageId != session.MessageId)
                {
                    await bot.AnswerCallbackQuery(callback.Id, Constants.OldMenu, cancellationToken: ct);
                    try { await bot.DeleteMessage(chatId, callback.Message!.MessageId, ct); } catch {}
                    return States.StateMenu;
                }
            }

            // B. Handle Save/Exit
            if (data.EndsWith("+SAVE"))
            {
                await bot.AnswerCallbackQuery(callback.Id, Constants.Done, cancellationToken: ct);
                try {
                    await bot.EditMessageText(chatId, callback.Message!.MessageId, Constants.Done, cancellationToken: ct);
                } catch {} 
                return null;
            }

            // C. Handle Logic
            return await HandleCallbackLogic(bot, chatId, callback, context, ct);
        }

        // 3. STATE MACHINE
        switch (context.CurrentState)
        {
            case States.StateAwaitingWQuota:
                return await HandleQuotaInput(bot, update, context, isWQuota: true, ct);

            case States.StateAwaitingPQuota:
                return await HandleQuotaInput(bot, update, context, isWQuota: false, ct);
            
            default:
                return States.StateMenu;
        }
    }

    // --- LOGIC HANDLERS ---

    private async Task<string> HandleCallbackLogic(ITelegramBotClient bot, long chatId, CallbackQuery query, ConversationContext context, CancellationToken ct)
    {
        var data = query.Data!; 
        var parts = data.Split('+');
        
        string type = parts.Length > 2 ? parts[2] : "";
        string action = parts.Length > 3 ? parts[3] : "";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        
        if (!long.TryParse(parts[1], out long targetGroupId)) targetGroupId = chatId;
        
        var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == targetGroupId, ct);
        if (group == null)
        {
            await bot.AnswerCallbackQuery(query.Id, "找不到群組資料。", cancellationToken: ct);
            return States.StateMenu;
        }

        bool refreshMenu = true;
        string nextState = States.StateMenu;

        switch (type)
        {
            case Types.WQuota:
                if (action == Actions.Add) group.WQuota++;
                else if (action == Actions.Minus) group.WQuota--;
                else if (action == Actions.Change)
                {
                    await bot.SendMessage(chatId, Constants.HowManyWine, replyMarkup: new ForceReplyMarkup(), cancellationToken: ct);
                    return States.StateAwaitingWQuota; 
                }
                break;

            case Types.PQuota:
                if (action == Actions.Add) group.PQuota++;
                else if (action == Actions.Minus) group.PQuota--;
                else if (action == Actions.Change)
                {
                    await bot.SendMessage(chatId, Constants.HowManyPlastic, replyMarkup: new ForceReplyMarkup(), cancellationToken: ct);
                    return States.StateAwaitingPQuota; 
                }
                break;

            case Types.Fortune:
                group.OffFortune = !group.OffFortune;
                await bot.AnswerCallbackQuery(query.Id, group.OffFortune ? Constants.TurnedOff : Constants.TurnedOn, cancellationToken: ct);
                break;
            case Types.Zodiac:
                group.OffZodiac = !group.OffZodiac;
                await bot.AnswerCallbackQuery(query.Id, group.OffZodiac ? Constants.TurnedOff : Constants.TurnedOn, cancellationToken: ct);
                break;
            case Types.Lomo:
                group.OffLomo = !group.OffLomo;
                await bot.AnswerCallbackQuery(query.Id, group.OffLomo ? Constants.TurnedOff : Constants.TurnedOn, cancellationToken: ct);
                break;
            case Types.Simp:
                group.OffSimp = !group.OffSimp;
                await bot.AnswerCallbackQuery(query.Id, group.OffSimp ? Constants.TurnedOff : Constants.TurnedOn, cancellationToken: ct);
                break;
            
            case "NULL":
                await bot.AnswerCallbackQuery(query.Id, Constants.InvalidButton, showAlert: true, cancellationToken: ct);
                refreshMenu = false;
                break;
        }

        if (refreshMenu)
        {
            await db.SaveChangesAsync(ct);
            try 
            {
                await bot.EditMessageReplyMarkup(chatId, query.Message!.MessageId, 
                    replyMarkup: GenerateKeyboard(group), 
                    cancellationToken: ct);
            }
            catch (Exception) { }
        }

        return nextState;
    }

    private async Task<string> HandleQuotaInput(ITelegramBotClient bot, Update update, ConversationContext context, bool isWQuota, CancellationToken ct)
    {
        var msg = update.Message;
        if (msg == null || string.IsNullOrWhiteSpace(msg.Text)) return isWQuota ? States.StateAwaitingWQuota : States.StateAwaitingPQuota;

        if (!int.TryParse(msg.Text, out int newValue) || newValue < 1 || newValue > 20)
        {
            string typeName = isWQuota ? "酒數" : "膠數";
            await bot.SendMessage(msg.Chat.Id, $"{typeName}最少係 1 ，最多係 20 。請重新輸入。", replyMarkup: new ForceReplyMarkup(), cancellationToken: ct);
            return isWQuota ? States.StateAwaitingWQuota : States.StateAwaitingPQuota;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        
        long targetGroupId = msg.Chat.Id;
        SettingsSession? session = null;
        
        if (context.Data.TryGetValue("Session", out var s) && s is SettingsSession sess)
        {
            session = sess;
            targetGroupId = session.TargetGroupId;
        }

        var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == targetGroupId, ct);
        if (group != null)
        {
            int oldValue = isWQuota ? group.WQuota : group.PQuota;
            if (oldValue == newValue)
            {
                await bot.SendMessage(msg.Chat.Id, $"本身已經係 {newValue} 喇喎，砍掉重煉！請輸入：", replyMarkup: new ForceReplyMarkup(), cancellationToken: ct);
                return isWQuota ? States.StateAwaitingWQuota : States.StateAwaitingPQuota;
            }

            if (isWQuota) group.WQuota = newValue;
            else group.PQuota = newValue;

            await db.SaveChangesAsync(ct);

            await bot.Reply(msg, Constants.Updated, ct);
            
            if (session != null)
            {
                try
                {
                    await bot.EditMessageReplyMarkup(msg.Chat.Id, session.MessageId, 
                        replyMarkup: GenerateKeyboard(group), 
                        cancellationToken: ct);
                }
                catch { }
            }
        }

        return States.StateMenu;
    }

    // --- HELPERS ---

    private async Task<string?> SendMainMenuAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct)
    {
        long originChatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        long userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id ?? 0;

        if (originChatId == 0 || userId == 0) return null; 

        long targetGroupId = originChatId;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        
        var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == targetGroupId, ct);
        if (group == null)
        {
            await bot.SendMessage(originChatId, "⚠️ 找不到此群組的資料 (請先在群組內發言以初始化)", cancellationToken: ct);
            return null;
        }

        bool isGroupChat = update.Message?.Chat.Type != ChatType.Private && update.CallbackQuery == null;

        if (isGroupChat)
        {
            await bot.Reply(update.Message, "呢啲野，我轉頭同你私底下傾啦 🙊", ct);
            
            try 
            {
                await bot.SendChatAction(userId, ChatAction.Typing, cancellationToken: ct);
            }
            catch (ApiRequestException)
            {
                var me = await bot.GetMe(ct);
                var kb = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("撳呢度！", $"http://telegram.me/{me.Username}?start=settings"));
                await bot.SendMessage(originChatId, "你好似未同我講過野喎，不如撳呢個制 start 左我先？", replyMarkup: kb, cancellationToken: ct);
                return null;
            }
        }

        string title = "此群組"; 
        try { var chat = await bot.GetChat(targetGroupId, ct); title = chat.Title ?? "此群組"; } catch {}

        string text = $"【{title}】\n\n注意一︰直接撳酒膠個數字，之後可以直接打數字。\n注意二︰最底三行顯示嘅係而家嘅狀態";

        var sentMsg = await bot.SendMessage(userId, text, replyMarkup: GenerateKeyboard(group), cancellationToken: ct);

        context.MenuMessageId = sentMsg.MessageId; // <--- 補上這行
        context.Data["Session"] = new SettingsSession 
        { 
            MessageId = sentMsg.MessageId,
            TargetGroupId = targetGroupId
        };

        return States.StateMenu;
    }

    private InlineKeyboardMarkup GenerateKeyboard(BotGroup group)
    {
        long id = group.TelegramId;
        var rows = new List<IEnumerable<InlineKeyboardButton>>();

        // --- 1. WINE QUOTA ---
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(Constants.ButtonWineLimit, $"SETTINGS+{id}+NULL") });

        var wineRow = new List<InlineKeyboardButton>();
        if (group.WQuota <= 1)
            wineRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonEmpty, $"SETTINGS+{id}+NULL"));
        else
            wineRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonMinus, $"SETTINGS+{id}+{Types.WQuota}+{Actions.Minus}"));
        
        wineRow.Add(InlineKeyboardButton.WithCallbackData($"{group.WQuota} {Constants.WineQuantity}", $"SETTINGS+{id}+{Types.WQuota}+{Actions.Change}"));

        if (group.WQuota >= 20)
            wineRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonEmpty, $"SETTINGS+{id}+NULL"));
        else
            wineRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonPlus, $"SETTINGS+{id}+{Types.WQuota}+{Actions.Add}"));
        
        rows.Add(wineRow);

        // --- 2. PLASTIC QUOTA ---
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(Constants.ButtonPlasticLimit, $"SETTINGS+{id}+NULL") });

        var plasticRow = new List<InlineKeyboardButton>();
        if (group.PQuota <= 1)
            plasticRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonEmpty, $"SETTINGS+{id}+NULL"));
        else
            plasticRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonMinus, $"SETTINGS+{id}+{Types.PQuota}+{Actions.Minus}"));

        plasticRow.Add(InlineKeyboardButton.WithCallbackData($"{group.PQuota} {Constants.PlasticQuantity}", $"SETTINGS+{id}+{Types.PQuota}+{Actions.Change}"));

        if (group.PQuota >= 20)
            plasticRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonEmpty, $"SETTINGS+{id}+NULL"));
        else
            plasticRow.Add(InlineKeyboardButton.WithCallbackData(Constants.ButtonPlus, $"SETTINGS+{id}+{Types.PQuota}+{Actions.Add}"));
            
        rows.Add(plasticRow);

        // --- 3. TOGGLES ---
        string GetStateText(bool isOff) => !isOff ? Constants.On : Constants.Off;

        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData(Constants.ButtonFortune, $"SETTINGS+{id}+NULL"),
            InlineKeyboardButton.WithCallbackData(GetStateText(group.OffFortune), $"SETTINGS+{id}+{Types.Fortune}")
        });

        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData(Constants.ButtonZodiac, $"SETTINGS+{id}+NULL"),
            InlineKeyboardButton.WithCallbackData(GetStateText(group.OffZodiac), $"SETTINGS+{id}+{Types.Zodiac}")
        });

        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData(Constants.ButtonLomo, $"SETTINGS+{id}+NULL"),
            InlineKeyboardButton.WithCallbackData(GetStateText(group.OffLomo), $"SETTINGS+{id}+{Types.Lomo}")
        });

        rows.Add(new[] {
            InlineKeyboardButton.WithCallbackData(Constants.ButtonSimp, $"SETTINGS+{id}+NULL"),
            InlineKeyboardButton.WithCallbackData(GetStateText(group.OffSimp), $"SETTINGS+{id}+{Types.Simp}")
        });

        // --- 4. SAVE ---
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(Constants.ButtonDone, $"SETTINGS+{id}+SAVE") });

        return new InlineKeyboardMarkup(rows);
    }
    
    // --- CONSTANTS ---
    private static class Constants
    {
        public const string On  = "開 ✅";
        public const string Off = "關 🚫";
        public const string TurnedOn = "開咗啦 ✅";
        public const string TurnedOff = "關咗啦 🚫";
        public const string Done = "搞掂，食碗麵。";
        public const string OldMenu = "呢個 Menu 係舊架喎，我幫你收走佢先啦。";
        public const string HowManyWine = "你想將呢個谷嘅每日酒數改做幾多？請輸入數字。";
        public const string HowManyPlastic = "你想將呢個谷嘅每日膠數改做幾多？請輸入數字。";
        public const string InvalidButton = "呢個制廢架，想走果時記得撳 '搞掂 🔚'！";
        public const string Updated = "⏫ 我幫你更新左喇，望下上面 ⏫";

        public const string ButtonFortune = "求籤 🙏";
        public const string ButtonZodiac = "星座 🔮";
        public const string ButtonLomo = "驅逐 5P 🖕";
        public const string ButtonSimp = "驅逐殘體 🇨🇳";
        public const string ButtonDone = "搞掂 🔚";

        public const string ButtonPlus = "➕";
        public const string ButtonMinus = "➖";
        public const string ButtonEmpty = "　";
        public const string ButtonWineLimit = "每日酒數 🍻";
        public const string ButtonPlasticLimit = "每日膠數 🌚";
        public const string WineQuantity = "杯";
        public const string PlasticQuantity = "粒";
    }

    private static class States
    {
        public const string StateStart = "Start";
        public const string StateMenu = "Menu";
        public const string StateAwaitingWQuota = "AwaitingWQuota";
        public const string StateAwaitingPQuota = "AwaitingPQuota";
    }

    private static class Types
    {
        public const string WQuota = "WQUOTA";
        public const string PQuota = "PQUOTA";
        public const string Fortune = "FORTUNE";
        public const string Zodiac = "ZODIAC";
        public const string Lomo = "LOMO";
        public const string Simp = "SIMP";
    }

    private static class Actions
    {
        public const string Add = "PLUS"; 
        public const string Minus = "MINUS";
        public const string Change = "CHANGE";
    }
}