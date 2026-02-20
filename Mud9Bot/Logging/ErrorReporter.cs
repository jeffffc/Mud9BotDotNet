using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Logging;

public class ErrorReporter(
    ITelegramBotClient botClient,
    ILogger<ErrorReporter> logger,
    IConfiguration configuration) : IErrorReporter
{
    private readonly long _logGroupId = configuration.GetValue<long>("BotConfiguration:LogGroupId");

    public async Task ReportErrorAsync(Exception exception, Message? message, CancellationToken ct = default)
    {
        // 1. Log to Console/System
        logger.LogError(exception, "Exception occurred processing update");

        // 2. Reply to User (if message context exists)
        // We catch errors here to ensure the original error is still logged even if the reply fails
        if (message != null)
        {
            try
            {
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "⚠️ Error happened",
                    replyParameters: new ReplyParameters { MessageId = message.MessageId },
                    cancellationToken: ct);
            }
            catch (Exception replyEx)
            {
                logger.LogError(replyEx, "Failed to reply to user about error.");
            }
        }

        // 3. Send to Log Group (if configured)
        if (_logGroupId != 0)
        {
            try
            {
                var user = message?.From;
                var chat = message?.Chat;

                // Escape values for MarkdownV2 using your StringExtensions
                var commandName = message?.Text?.Split(' ').FirstOrDefault().EscapeHtml() ?? "Unknown";
                var userName = (user?.FirstName ?? "Unknown").EscapeHtml();
                var userId = user?.Id.ToString().EscapeHtml() ?? "Unknown";
                var chatTitle = (chat?.Title ?? "Private").EscapeHtml();
                var chatId = chat?.Id.ToString().EscapeHtml() ?? "Unknown";
                
                var actualEx = exception.InnerException ?? exception;
                var errorText = actualEx.Message.EscapeHtml();
                var stackTrace = (actualEx.StackTrace ?? "No StackTrace").EscapeHtml();

                // Truncate stack trace to prevent hitting Telegram's 4096 char limit
                if (stackTrace.Length > 1500) 
                    stackTrace = stackTrace.Substring(0, 1500) + "...";

                var logMessage = $"🚨 <b>Exception in Command:</b> <code>{commandName}</code>\n" +
                                 $"<b>User:</b> <code>{userName} ({userId})</code>\n" +
                                 $"<b>Chat:</b> <code>{chatTitle} ({chatId})</code>\n\n" +
                                 $"<b>Error:</b> <code>{errorText}</code>\n" +
                                 $"<b>Stack:</b> <pre>{stackTrace}</pre>";

                await botClient.SendMessage(
                    chatId: _logGroupId,
                    text: logMessage,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
            catch (Exception logEx)
            {
                logger.LogError(logEx, "Failed to send error log to group.");
            }
        }
    }
}