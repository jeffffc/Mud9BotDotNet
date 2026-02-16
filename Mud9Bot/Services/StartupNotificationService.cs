using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using System.Reflection; // Added for version info

namespace Mud9Bot.Services;

public class StartupNotificationService(
    ITelegramBotClient botClient,
    IConfiguration configuration,
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
            
            var message = $"🤖 *Bot Started*\n" +
                          $"Name: `{me.FirstName}`\n" +
                          $"Username: @{me.Username}\n" +
                          $"Version: `{version}`\n" +
                          $"Time: `{startTime}`";

            await botClient.SendMessage(
                chatId: _logGroupId,
                text: message,
                parseMode: ParseMode.MarkdownV2,
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
            var message = $"🛑 *Bot Stopping*\n" +
                          $"Time: `{stopTime}`";

            await botClient.SendMessage(
                chatId: _logGroupId,
                text: message,
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: cancellationToken);
                
            logger.LogInformation($"Shutdown notification sent to {_logGroupId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send shutdown notification");
        }
    }
}