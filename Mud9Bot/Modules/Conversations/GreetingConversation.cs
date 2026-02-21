using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

// Moved DevOnly logic to the Attribute
[Conversation("greetings", Description = "管理專屬問候語", DevOnly = true)]
public class GreetingConversation : IConversation
{
    public string ConversationName => "GreetingFlow";
    
    // Catch callbacks starting with GREETINGS+
    public bool IsEntryPoint(Update update) => update.CallbackQuery?.Data?.StartsWith("GREETINGS+") ?? false;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGreetingService _greetingService;

    // Removed IConfiguration since we no longer need _devIds here
    public GreetingConversation(IServiceScopeFactory scopeFactory, IGreetingService greetingService)
    {
        _scopeFactory = scopeFactory;
        _greetingService = greetingService;
    }

    private class GreetingSession
    {
        public int MessageId { get; set; }
        public long TargetUserId { get; set; }
    }

    public async Task<string?> ExecuteStepAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct)
    {
        // Centralized Manager already did the Security Check! We can proceed directly.
        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
        var callback = update.CallbackQuery;

        // Rehydration (Allows continuing if the bot was restarted)
        if (context.CurrentState == "Start" && callback != null && callback.Data is { } cbData && cbData.StartsWith("GREETINGS+"))
        {
            var parts = cbData.Split('+');
            if (parts.Length >= 2 && long.TryParse(parts[1], out long savedUserId))
            {
                int msgId = callback.Message?.MessageId ?? 0;
                context.MenuMessageId = msgId; // <--- 補上這行
                context.Data["Session"] = new GreetingSession 
                { 
                    MessageId = msgId,
                    TargetUserId = savedUserId 
                };
                context.CurrentState = "Menu"; 
            }
        }

        // ---------------------------------------------------------
        // 1. ENTRY POINT (Triggered via /greetings)
        // ---------------------------------------------------------
        if (context.CurrentState == "Start")
        {
            long targetUserId = 0;
            
            // Allow triggering by Replying to the target user's message
            if (update.Message?.ReplyToMessage != null)
            {
                targetUserId = update.Message.ReplyToMessage.From!.Id;
            }
            else // Or by parsing the argument
            {
                var text = update.Message?.Text;
                var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts != null && parts.Length > 1) long.TryParse(parts[1], out targetUserId);
            }

            if (targetUserId == 0)
            {
                await bot.SendMessage(
                    chatId, 
                    "你想管理邊個嘅問候語？\n請對住佢嘅訊息 Reply <code>/greetings</code>，或者直接打 <code>/greetings [UserID]</code>！", 
                    parseMode: ParseMode.Html, 
                    cancellationToken: ct);
                return null;
            }

            context.Data["Session"] = new GreetingSession { TargetUserId = targetUserId };
            return await SendMainMenuAsync(bot, chatId, targetUserId, context, ct);
        }

        // ---------------------------------------------------------
        // 2. CALLBACK HANDLER (Menu Navigation)
        // ---------------------------------------------------------
        if (callback != null && callback.Data is { } data && data.StartsWith("GREETINGS+"))
        {
            if (context.Data.TryGetValue("Session", out var sessionObj) && sessionObj is GreetingSession session)
            {
                if (callback.Message?.MessageId != session.MessageId)
                {
                    await bot.AnswerCallbackQuery(callback.Id, "呢個 Menu 已經過期。", cancellationToken: ct);
                    try { await bot.DeleteMessage(chatId, callback.Message!.MessageId, ct); } catch {}
                    return "Menu";
                }

                var parts = data.Split('+');
                if (parts.Length < 3) return "Menu";
                
                string action = parts[2];

                if (action == "CLOSE")
                {
                    try { await bot.DeleteMessage(chatId, session.MessageId, ct); } catch {}
                    return null;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                if (action == "MAIN")
                {
                    await EditToMainMenuAsync(bot, chatId, session.MessageId, session.TargetUserId, db, ct);
                }
                else if (action == "LIST" && parts.Length >= 4)
                {
                    await RenderListAsync(bot, chatId, session.MessageId, session.TargetUserId, parts[3], db, ct);
                }
                else if (action == "DEL" && parts.Length >= 5)
                {
                    if (int.TryParse(parts[3], out int delId))
                    {
                        var toDelete = await db.Set<CustomGreeting>().FindAsync(delId);
                        if (toDelete != null)
                        {
                            db.Set<CustomGreeting>().Remove(toDelete);
                            await db.SaveChangesAsync(ct);
                            
                            // Instant Cache Refresh!
                            await _greetingService.InitializeAsync();
                        }
                        await bot.AnswerCallbackQuery(callback.Id, "🗑 刪除成功！", cancellationToken: ct);
                        await RenderListAsync(bot, chatId, session.MessageId, session.TargetUserId, parts[4], db, ct);
                    }
                }
                else if (action == "ADD" && parts.Length >= 4)
                {
                    string typeName = parts[3] == "MORNING" ? "早晨" : "晚安";
                    await bot.SendMessage(
                        chatId, 
                        $"請輸入要新增畀 <code>{session.TargetUserId}</code> 嘅<b>{typeName}</b>訊息：（支援 Emoji）\n或輸入 /cancel 取消。", 
                        parseMode: ParseMode.Html, 
                        cancellationToken: ct);
                        
                    return parts[3] == "MORNING" ? "AwaitingAddMorning" : "AwaitingAddNight";
                }
            }
            return "Menu";
        }

        // ---------------------------------------------------------
        // 3. AWAITING INPUT (Adding a new message)
        // ---------------------------------------------------------
        if (context.CurrentState == "AwaitingAddMorning" || context.CurrentState == "AwaitingAddNight")
        {
            var text = update.Message?.Text;
            if (string.IsNullOrWhiteSpace(text)) return context.CurrentState;

            if (context.Data.TryGetValue("Session", out var sessionObj) && sessionObj is GreetingSession session)
            {
                if (text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                {
                    await bot.SendMessage(chatId, "已取消新增。", cancellationToken: ct);
                    return await SendMainMenuAsync(bot, chatId, session.TargetUserId, context, ct);
                }

                string type = context.CurrentState == "AwaitingAddMorning" ? "MORNING" : "NIGHT";

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                // --- DUPLICATE CHECK ---
                bool exists = await db.Set<CustomGreeting>().AnyAsync(g => g.TelegramId == session.TargetUserId && g.GreetingType == type && g.Content == text, ct);
                if (exists)
                {
                    await bot.SendMessage(chatId, "⚠️ 呢句問候語已經存在啦！請輸入過第二句，或者輸入 /cancel 取消。", cancellationToken: ct);
                    return context.CurrentState;
                }
                // -----------------------

                db.Set<CustomGreeting>().Add(new CustomGreeting
                {
                    TelegramId = session.TargetUserId,
                    GreetingType = type,
                    Content = text
                });

                await db.SaveChangesAsync(ct);
                
                // Instant Cache Refresh!
                await _greetingService.InitializeAsync();

                await bot.SendMessage(chatId, "✅ 訊息新增成功！", cancellationToken: ct);
                return await SendMainMenuAsync(bot, chatId, session.TargetUserId, context, ct);
            }
        }

        return "Menu";
    }

    // --- Helpers ---

    private async Task<string> SendMainMenuAsync(ITelegramBotClient bot, long chatId, long targetUserId, ConversationContext context, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        int mCount = await db.Set<CustomGreeting>().CountAsync(g => g.TelegramId == targetUserId && g.GreetingType == "MORNING", ct);
        int nCount = await db.Set<CustomGreeting>().CountAsync(g => g.TelegramId == targetUserId && g.GreetingType == "NIGHT", ct);

        string text = $"👤 <b>管理專屬問候語</b>\n\n目標用戶：<code>{targetUserId}</code>\n🌞 早晨訊息：{mCount} 條\n🌙 晚安訊息：{nCount} 條";

        var msg = await bot.SendMessage(chatId, text, parseMode: ParseMode.Html, replyMarkup: GetMainKeyboard(targetUserId), cancellationToken: ct);
        
        context.MenuMessageId = msg.MessageId; // <--- 補上這行
        if (context.Data.TryGetValue("Session", out var sessionObj) && sessionObj is GreetingSession session)
        {
            session.MessageId = msg.MessageId;
        }

        return "Menu";
    }

    private async Task EditToMainMenuAsync(ITelegramBotClient bot, long chatId, int messageId, long targetUserId, BotDbContext db, CancellationToken ct)
    {
        int mCount = await db.Set<CustomGreeting>().CountAsync(g => g.TelegramId == targetUserId && g.GreetingType == "MORNING", ct);
        int nCount = await db.Set<CustomGreeting>().CountAsync(g => g.TelegramId == targetUserId && g.GreetingType == "NIGHT", ct);

        string text = $"👤 <b>管理專屬問候語</b>\n\n目標用戶：<code>{targetUserId}</code>\n🌞 早晨訊息：{mCount} 條\n🌙 晚安訊息：{nCount} 條";

        try
        {
            await bot.EditMessageText(chatId, messageId, text, parseMode: ParseMode.Html, replyMarkup: GetMainKeyboard(targetUserId), cancellationToken: ct);
        }
        catch (ApiRequestException) { }
    }

    private async Task RenderListAsync(ITelegramBotClient bot, long chatId, int messageId, long targetUserId, string type, BotDbContext db, CancellationToken ct)
    {
        var items = await db.Set<CustomGreeting>()
            .Where(g => g.TelegramId == targetUserId && g.GreetingType == type)
            .OrderBy(g => g.Id)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"👤 <b>管理專屬問候語</b> - {(type == "MORNING" ? "🌞 早晨" : "🌙 晚安")}\n");

        if (!items.Any()) sb.AppendLine("<i>暫無任何訊息。</i>");
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                sb.AppendLine($"<b>{i + 1}.</b> {items[i].Content.EscapeHtml()}");
            }
        }

        var buttons = new List<InlineKeyboardButton>();
        for (int i = 0; i < items.Count; i++)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData($"🗑 刪除 {i + 1}", $"GREETINGS+{targetUserId}+DEL+{items[i].Id}+{type}"));
        }

        // Chunk by 3 buttons per row
        var rows = buttons.Chunk(3).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 返回主目錄", $"GREETINGS+{targetUserId}+MAIN") });

        try
        {
            await bot.EditMessageText(chatId, messageId, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }
        catch (ApiRequestException) { }
    }

    private InlineKeyboardMarkup GetMainKeyboard(long targetUserId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] {
                InlineKeyboardButton.WithCallbackData("🌞 管理早晨", $"GREETINGS+{targetUserId}+LIST+MORNING"),
                InlineKeyboardButton.WithCallbackData("🌙 管理晚安", $"GREETINGS+{targetUserId}+LIST+NIGHT")
            },
            new[] {
                InlineKeyboardButton.WithCallbackData("➕ 新增早晨", $"GREETINGS+{targetUserId}+ADD+MORNING"),
                InlineKeyboardButton.WithCallbackData("➕ 新增晚安", $"GREETINGS+{targetUserId}+ADD+NIGHT")
            },
            new[] { InlineKeyboardButton.WithCallbackData("🔚 關閉選單", $"GREETINGS+{targetUserId}+CLOSE") }
        });
    }
}