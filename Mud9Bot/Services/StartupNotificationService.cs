using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using System.Reflection;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class StartupNotificationService(
    ITelegramBotClient botClient,
    IConfiguration configuration,
    IBotMetadataService metadata, 
    ILogger<StartupNotificationService> logger) : IHostedService
{
    private readonly long _logGroupId = configuration.GetValue<long>("BotConfiguration:LogGroupId");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_logGroupId == 0) return;

        try
        {
            var me = await botClient.GetMe(cancellationToken);
            var startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            
            // Added Msg Triggers count to the Markdown notification
            var message = $"🤖 *Bot Started*\n" +
                          $"Name: `{me.FirstName}`\n" +
                          $"Version: `{version}`\n" +
                          $"Time: `{startTime}`\n\n" +
                          $"📊 *Registration Stats*\n" +
                          $"├ Commands: `{metadata.CommandCount}`\n" +
                          $"├ Callbacks: `{metadata.CallbackCount}`\n" +
                          $"├ Msg Triggers: `{metadata.MessageTriggerCount}`\n" +
                          $"├ Jobs: `{metadata.JobCount}`\n" +
                          $"├ Services: `{metadata.ServiceCount}`\n" +
                          $"└ Conversations: `{metadata.ConversationCount}`";

            await botClient.SendMessage(
                chatId: _logGroupId,
                text: message,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
            
            logger.LogInformation("Startup notification sent.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send startup notification");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_logGroupId == 0) return;
        try
        {
            var stopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await botClient.SendMessage(_logGroupId, $"🛑 *Bot Stopping*\nTime: `{stopTime}`", parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
        }
        catch { }
    }
}