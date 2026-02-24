using System.Reflection;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

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
            var startTime = DateTime.Now.ToHkTime().ToString("yyyy-MM-dd HH:mm:ss");
            
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";
            
            // 🚀 Get the DLL's last write time (Compilation/Publish time)
            var buildTime = System.IO.File.GetLastWriteTime(assembly.Location).ToHkTime();
            
            // Construct the message using HTML tags
            var message = $"🤖 <b>Bot Started</b>\n" +
                          $"Name: <code>{me.FirstName.EscapeHtml()}</code>\n" +
                          $"Version: <code>{version}</code>\n" +
                          $"Built At: <code>{buildTime:yyyy-MM-dd HH:mm:ss}</code>\n" +
                          $"Time: <code>{startTime}</code>\n\n" +
                          $"📊 <b>Registration Stats</b>\n" +
                          $"├ Commands: <code>{metadata.CommandCount}</code>\n" +
                          $"├ Callbacks: <code>{metadata.CallbackCount}</code>\n" +
                          $"├ Msg Triggers: <code>{metadata.MessageTriggerCount}</code>\n" +
                          $"├ Jobs: <code>{metadata.JobCount}</code>\n" +
                          $"├ Services: <code>{metadata.ServiceCount}</code>\n" +
                          $"└ Conversations: <code>{metadata.ConversationCount}</code>";

            await botClient.SendMessage(
                chatId: _logGroupId,
                text: message,
                parseMode: ParseMode.Html, // 🚀 Swapped to HTML
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
            var stopTime = DateTime.Now.ToHkTime().ToString("yyyy-MM-dd HH:mm:ss");
            
            // Also update the stopping message to HTML
            string stopMsg = $"🛑 <b>Bot Stopping</b>\nTime: <code>{stopTime}</code>";
            
            await botClient.SendMessage(
                _logGroupId, 
                stopMsg, 
                parseMode: ParseMode.Html, 
                cancellationToken: cancellationToken);
        }
        catch { }
    }
}