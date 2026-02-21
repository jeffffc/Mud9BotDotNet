using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;

namespace Mud9Bot.Modules.Conversations;

[Conversation("myreminders", Description = "管理你嘅提醒事項")]
public class ReminderManagementConversation : IConversation
{
    public string ConversationName => "ReminderManagementFlow";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReminderService _reminderService;
    private static readonly TimeZoneInfo HkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    public ReminderManagementConversation(IServiceScopeFactory scopeFactory, IReminderService reminderService)
    {
        _scopeFactory = scopeFactory;
        _reminderService = reminderService;
    }

    public bool IsEntryPoint(Update update) 
        => update.CallbackQuery?.Data?.StartsWith("MYREMINDERS+") ?? false;

    public async Task<string?> ExecuteStepAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct)
    {
        var originChatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        var callback = update.CallbackQuery;
        var userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id ?? 0;

        if (userId == 0) return null;

        // ---------------------------------------------------------
        // 1. 進入點：處理群組轉私訊邏輯
        // ---------------------------------------------------------
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
                    string resultState = await SendManagementMenuAsync(bot, userId, userId, context, ct, isEdit: false);
                    await bot.Reply(update.Message, "呢啲野，我轉頭同你私底下傾啦 🙊", ct);
                    context.ChatId = userId; 
                    return resultState;
                }
                catch (ApiRequestException ex) when (ex.ErrorCode == 403)
                {
                    var me = await bot.GetMe(ct);
                    var kb = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("撳呢度啟動我！ 🚀", $"http://telegram.me/{me.Username}?start=myreminders"));
                    
                    await bot.SendMessage(
                        chatId: originChatId,
                        text: "你好似未同我講過野喎，不如撳呢個制啟動咗我，我再話畀你聽？",
                        replyMarkup: kb,
                        replyParameters: new ReplyParameters { MessageId = update.Message.MessageId },
                        cancellationToken: ct
                    );
                    
                    return null;
                }
            }
            else
            {
                return await SendManagementMenuAsync(bot, originChatId, userId, context, ct);
            }
        }

        // ---------------------------------------------------------
        // 2. 選單按鈕處理 (Callback)
        // ---------------------------------------------------------
        if (callback != null && callback.Data is { } data && data.StartsWith("MYREMINDERS+"))
        {
            string? hint = null;
            // 🚀 重入點邏輯：如果是點擊舊訊息，自動同步 ID 並顯示提示
            if (context.MenuMessageId != 0 && callback.Message?.MessageId != context.MenuMessageId)
            {
                hint = "⚠️ <i>你頭先撳嗰個係舊選單，我已經幫你更新咗做最新嘅資料，請再揀過。</i>\n\n";
                context.MenuMessageId = callback.Message?.MessageId ?? 0;
            }
            else if (context.MenuMessageId == 0)
            {
                context.MenuMessageId = callback.Message?.MessageId ?? 0;
            }

            var parts = data.Split('+');
            if (parts.Length < 2) return "Menu";
            
            string action = parts[1];

            if (action == "CLOSE")
            {
                await bot.AnswerCallbackQuery(callback.Id, "搞掂！", cancellationToken: ct);
                try { await bot.EditMessageText(originChatId, callback.Message!.MessageId, "搞掂，食碗麵。🔚", cancellationToken: ct); } catch {}
                return null; 
            }

            if (action == "DEL" && parts.Length >= 3)
            {
                if (int.TryParse(parts[2], out int jobId))
                {
                    bool deleted = await _reminderService.DeleteReminderAsync(jobId, userId);
                    string toast = deleted ? "🗑 已刪除提醒！" : "❌ 搵唔到呢個提醒。";
                    await bot.AnswerCallbackQuery(callback.Id, toast, cancellationToken: ct);
                    return await SendManagementMenuAsync(bot, originChatId, userId, context, ct, isEdit: true, hint: hint);
                }
            }
            
            if (action == "REFRESH")
            {
                await bot.AnswerCallbackQuery(callback.Id, "🔄 已更新列表", cancellationToken: ct);
                return await SendManagementMenuAsync(bot, originChatId, userId, context, ct, isEdit: true, hint: hint);
            }

            return await SendManagementMenuAsync(bot, originChatId, userId, context, ct, isEdit: true, hint: hint);
        }

        return "Menu";
    }

    private async Task<string> SendManagementMenuAsync(ITelegramBotClient bot, long chatId, long userId, ConversationContext context, CancellationToken ct, bool isEdit = false, string? hint = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        var activeReminders = await db.Set<Job>()
            .Where(j => j.TelegramId == userId && !j.IsProcessed)
            .OrderBy(j => j.Time)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(hint)) sb.Append(hint);

        sb.AppendLine("⏰ <b>你嘅生效中提醒事項</b>");
        sb.AppendLine($"<i>(每人上限 30 條，目前：{activeReminders.Count}/30)</i>\n");

        if (!activeReminders.Any())
        {
            sb.AppendLine("你暫時冇任何生效中嘅提醒事項。");
        }
        else
        {
            for (int i = 0; i < activeReminders.Count; i++)
            {
                var r = activeReminders[i];
                string timeStr;
                if (!string.IsNullOrEmpty(r.Recurrence))
                {
                    string type = r.Recurrence == "DAILY" ? "每日" : $"逢{MapWeekday(r.Recurrence)}";
                    var hkTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.Time, DateTimeKind.Utc), HkTimeZone);
                    timeStr = $"[🔄 {type} {hkTime:HH:mm}]";
                }
                else
                {
                    var hkTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.Time, DateTimeKind.Utc), HkTimeZone);
                    timeStr = $"[{hkTime:MM/dd HH:mm}]";
                }
                string content = r.Text ?? "無內容";
                if (content.Length > 15) content = content.Substring(0, 12) + "...";
                sb.AppendLine($"<b>{(i + 1)}.</b> {timeStr} {content.EscapeHtml()}");
            }
        }

        var buttons = new List<InlineKeyboardButton>();
        for (int i = 0; i < activeReminders.Count; i++)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData($"🗑 刪除 {i + 1}", $"MYREMINDERS+DEL+{(activeReminders[i].JobId)}"));
        }

        var rows = buttons.Chunk(3).ToList();
        rows.Add(new[] { 
            InlineKeyboardButton.WithCallbackData("🔄 刷新", "MYREMINDERS+REFRESH"),
            InlineKeyboardButton.WithCallbackData("🔚 關閉", "MYREMINDERS+CLOSE") 
        });

        try
        {
            if (isEdit && context.MenuMessageId != 0)
            {
                await bot.EditMessageText(chatId, context.MenuMessageId, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            }
            else
            {
                var msg = await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
                context.MenuMessageId = msg.MessageId;
            }
        }
        catch (ApiRequestException) { }

        return "Menu";
    }

    private string MapWeekday(string code) => code switch
    {
        "MON" => "星期一", "TUE" => "星期二", "WED" => "星期三", "THU" => "星期四",
        "FRI" => "星期五", "SAT" => "星期六", "SUN" => "星期日", _ => code
    };
}