using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot;

public class Worker(ILogger<Worker> logger, ITelegramBotClient botClient, IUpdateHandler updateHandler) : BackgroundService 
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation($"Mud9Bot started as @{me.Username}");

        // Start receiving updates
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>(), // receive all update types
            #if DEBUG
                DropPendingUpdates = true
            #else
                DropPendingUpdates = false
            #endif
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await botClient.ReceiveAsync(
                    updateHandler: updateHandler,
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Polling loop failed, retrying in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}