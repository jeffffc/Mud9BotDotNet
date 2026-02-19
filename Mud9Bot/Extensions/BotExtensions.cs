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
            text: Markdown.Escape(text),
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }

    // Shortcut: Reply with MarkdownV2
    // Usage: await bot.Reply(originalMessage, "text");
    public static async Task<Message> Reply(this ITelegramBotClient bot, Message originalMessage, string text, CancellationToken ct = default)
    {
        return await bot.SendMessage(
            chatId: originalMessage.Chat.Id,
            text: Markdown.Escape(text),
            parseMode: ParseMode.MarkdownV2,
            replyParameters: new ReplyParameters { MessageId = originalMessage.MessageId },
            cancellationToken: ct);
    }

    // New: Centralized Exception Logging
    public static async Task LogException(this ITelegramBotClient bot, Exception ex, Message? message, long logGroupId, ILogger logger, CancellationToken ct = default)
    {
        // 1. Log to Console/System
        logger.LogError(ex, "Exception occurred");

        // 2. Reply to User (if message context exists)
        if (message != null)
        {
            try 
            {
                await bot.Reply(message, "⚠️ Error happened", ct);
            }
            catch (Exception replyEx)
            {
                logger.LogError(replyEx, "Failed to reply to user about error.");
            }
        }

        // 3. Send to Log Group (if configured)
        if (logGroupId != 0)
        {
            var user = message?.From;
            var chat = message?.Chat;
            
            // Escape values for MarkdownV2
            var commandName = message?.Text?.Split(' ').FirstOrDefault()?.EscapeMarkdown() ?? "Unknown";
            var userName = (user?.FirstName ?? "Unknown").EscapeMarkdown();
            var userId = user?.Id.ToString().EscapeMarkdown() ?? "Unknown";
            var chatTitle = (chat?.Title ?? "Private").EscapeMarkdown();
            var chatId = chat?.Id.ToString().EscapeMarkdown() ?? "Unknown";
            var errorText = (ex.InnerException?.Message ?? ex.Message).EscapeMarkdown();
            var stackTrace = (ex.StackTrace ?? "No StackTrace").EscapeMarkdown();

            // Truncate stack trace if too long
            if (stackTrace.Length > 1000) stackTrace = stackTrace.Substring(0, 1000) + "\\.\\.\\.";

            var logMessage = $"🚨 *Exception in Command:* {commandName}\n" +
                             $"*User:* {userName} \\({userId}\\)\n" +
                             $"*Chat:* {chatTitle} \\({chatId}\\)\n\n" +
                             $"*Error:* `{errorText}`\n" +
                             $"*Stack:* `{stackTrace}`";

            try
            {
                await bot.Send(logGroupId, logMessage, ct);
            }
            catch (Exception logEx)
            {
                logger.LogError(logEx, "Failed to send error log to group.");
            }
        }
    }
}