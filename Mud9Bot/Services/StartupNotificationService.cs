using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using System.Reflection;
using Mud9Bot.Interfaces; // Added for version info

namespace Mud9Bot.Services;

public class StartupNotificationService(
    ITelegramBotClient botClient,
    IConfiguration configuration,
    IBotMetadataService metadata, // Injected
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
            
            var message = $"🤖 <b>Bot Started</b>\n" +
                          $"Name: <code>{me.FirstName}</code>\n" +
                          $"Version: <code>{version}</code>\n" +
                          $"Time: <code>{startTime}</code>\n\n" +
                          $"📊 <b>Registration Stats</b>\n" +
                          $"├ Commands: <code>{metadata.CommandCount}</code>\n" +
                          $"├ Callbacks: <code>{metadata.CallbackCount}</code>\n" +
                          $"├ Jobs: <code>{metadata.JobCount}</code>\n" +
                          $"├ Services: <code>{metadata.ServiceCount}</code>\n" +
                          $"└ Conversations: <code>{metadata.ConversationCount}</code>";

            await botClient.SendMessage(
                chatId: _logGroupId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            
            logger.LogInformation($"Startup notification sent to {_logGroupId}");
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
            var message = $"🛑 <b>Bot Stopping</b>\n" +
                          $"Time: <code>{stopTime}</code>";

            await botClient.SendMessage(
                chatId: _logGroupId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
                
            logger.LogInformation($"Shutdown notification sent to {_logGroupId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send shutdown notification");
        }
    }
}