using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Extensions;

public static class BotExtensions
{
    // Shortcut: Send MarkdownV2 message
    // Usage: await bot.Send(chatId, "text");
    public static async Task<Message> Send(this ITelegramBotClient bot, ChatId chatId, string text, CancellationToken ct = default)
    {
        return await bot.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    // Shortcut: Reply with MarkdownV2
    // Usage: await bot.Reply(originalMessage, "text");
    public static async Task<Message> Reply(this ITelegramBotClient bot, Message originalMessage, string text, CancellationToken ct = default)
    {
        return await bot.SendMessage(
            chatId: originalMessage.Chat.Id,
            text: text,
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = originalMessage.MessageId },
            cancellationToken: ct);
    }

    // New: Centralized Exception Logging
    public static async Task LogException(this ITelegramBotClient bot, Exception ex, Message? message, long logGroupId, ILogger logger, CancellationToken ct = default)
    {
        // 1. Standard System Logging
        logger.LogError(ex, "Exception occurred during bot operation.");

        // 2. User-facing Error Message
        if (message != null)
        {
            try 
            {
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: "⚠️ <b>發生系統錯誤</b>\n管理員已收到通知並會盡快處理。",
                    parseMode: ParseMode.Html,
                    replyParameters: new ReplyParameters { MessageId = message.MessageId },
                    cancellationToken: ct);
            }
            catch (Exception replyEx)
            {
                logger.LogError(replyEx, "Failed to reply to user about the exception.");
            }
        }

        // 3. Admin Log Group Notification
        if (logGroupId != 0)
        {
            var user = message?.From;
            var chat = message?.Chat;
            
            // Local helper using the requested extension method
            string Safe(string? s) => HtmlText.Escape(s ?? "Unknown");

            var commandName = Safe(message?.Text?.Split(' ').FirstOrDefault());
            var userName = Safe(user?.FirstName);
            var userId = user?.Id.ToString() ?? "Unknown";
            var chatTitle = Safe(chat?.Title ?? "Private Chat");
            var chatId = chat?.Id.ToString() ?? "Unknown";
            var errorText = Safe(ex.InnerException?.Message ?? ex.Message);
            var stackTrace = Safe(ex.StackTrace ?? "No StackTrace Available");

            // Truncate stack trace to stay within Telegram's message limits
            if (stackTrace.Length > 1500) stackTrace = stackTrace.Substring(0, 1500) + "... (Truncated)";

            var logMessage = $"🚨 <b>Exception in Command:</b> {commandName}\n" +
                             $"👤 <b>User:</b> {userName} (<code>{userId}</code>)\n" +
                             $"💬 <b>Chat:</b> {chatTitle} (<code>{chatId}</code>)\n\n" +
                             $"❌ <b>Error:</b> <code>{errorText}</code>\n\n" +
                             $"📑 <b>Stack Trace:</b>\n<pre>{stackTrace}</pre>";

            try
            {
                await bot.SendMessage(
                    chatId: logGroupId,
                    text: logMessage,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
            catch (Exception logEx)
            {
                logger.LogError(logEx, "Failed to send detailed error log to Admin Group.");
            }
        }
    }
}