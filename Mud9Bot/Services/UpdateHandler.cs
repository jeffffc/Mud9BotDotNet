using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Mud9Bot.Services.Registries;
using Mud9Bot.Services.Interfaces; // For IUserService

namespace Mud9Bot.Services;

// Inject IEnumerable<IBotCommand> to get ALL registered commands automatically
public class UpdateHandler(
    ILogger<UpdateHandler> logger, 
    CommandRegistry commandRegistry,
    CallbackQueryRegistry callbackRegistry,
    IServiceScopeFactory scopeFactory) : IUpdateHandler // Primary Constructor
{
    private string? _botUsername;
    
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } callbackQuery)
        {
            // Create a scope to resolve dependencies for the Callback Modules (FortuneService, DbContext, etc.)
            using var cbScope = scopeFactory.CreateScope();
            
            await callbackRegistry.ExecuteAsync(botClient, callbackQuery, cbScope.ServiceProvider, cancellationToken);
            return;
        }
        
        
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;
        // if (!messageText.StartsWith('/')) return; // <--- REMOVE THIS if you want to track all messages for "LastSeen", otherwise keep it.
        // For now, let's keep it to sync user on every command.

        var chatId = message.Chat.Id;

        // --- SCOPE CREATION ---
        // We create a scope to resolve Scoped services like IUserService and BotDbContext
        // Use 'scopeFactory' directly from the primary constructor
        using var scope = scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        
        // 1. Sync User & Group
        if (message.From != null)
        {
            await userService.SyncUserAsync(message.From, cancellationToken);
        }
        if (message.Chat.Type != ChatType.Private)
        {
            await userService.SyncGroupAsync(message.Chat, cancellationToken);
        }
        // ----------------------

        if (!messageText.StartsWith('/')) return; // Ignore non-commands after syncing

        // ... Command Parsing & Execution Logic ...
        
        // --- PARSING LOGIC START ---
        // 1. Split command and args: "/sql@Mud9Bot select * from table"
        var parts = messageText.Split(' ', 2); // Split into 2 parts max
        var commandPart = parts[0].Substring(1); // "sql@Mud9Bot"
        var args = parts.Length > 1 ? parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>(); 
        // Note: Previous logic used string[] for args in CommandRegistry, so we split here. 
        // If your logic expects the raw string rest, adjust accordingly. 
        // Based on previous turn, CommandRegistry expects string[].

        // 2. Handle @BotName logic
        var atIndex = commandPart.IndexOf('@');
        if (atIndex > 0)
        {
            var targetBot = commandPart.Substring(atIndex + 1); // "Mud9Bot"

            // Lazy load bot username if not cached
            if (string.IsNullOrEmpty(_botUsername))
            {
                var me = await botClient.GetMe(cancellationToken);
                _botUsername = me.Username;
            }

            // Ignore if command is meant for another bot
            if (!string.Equals(targetBot, _botUsername, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            commandPart = commandPart.Substring(0, atIndex); // "sql"
        }
        // --- PARSING LOGIC END ---

        logger.LogInformation($"Command detected: {commandPart}");

        // FIX: Update ExecuteAsync signature in CommandRegistry to accept IServiceProvider
        await commandRegistry.ExecuteAsync(commandPart, args, botClient, message, scope.ServiceProvider, cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, IFortuneService fortuneService, CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data;
        if (string.IsNullOrEmpty(data)) return;

        if (data.StartsWith("fortune:"))
        {
            if (int.TryParse(data.Split(':')[1], out int index))
            {
                var fortune = fortuneService.GetFortuneByIndex(index);
                if (fortune != null)
                {
                    await botClient.AnswerCallbackQuery(
                        callbackQueryId: callbackQuery.Id,
                        text: fortune.b,
                        showAlert: true,
                        cancellationToken: cancellationToken
                    );
                    return;
                }
            }
            await botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id, 
                text: "找不到該靈籤資料。", 
                cancellationToken: cancellationToken
            );
        }
        
        if (data.StartsWith("wp:") == true)
        {
            // Format: "wp:{action}:{senderId}:{targetId}"
            var parts = callbackQuery.Data!.Split(':');
            if (parts.Length != 4) return;

            var action = parts[1]; // "w" or "p"
            if (!long.TryParse(parts[2], out var originalSenderId)) return;
            if (!long.TryParse(parts[3], out var targetTelegramId)) return;

            // 1. Verify Clicker is the Original Sender
            if (callbackQuery.From.Id != originalSenderId)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "你唔係發起人，無權禁掣！", cancellationToken: cancellationToken);
                return;
            }

            // 2. Resolve Scoped Services
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var wpService = scope.ServiceProvider.GetRequiredService<IWinePlasticService>();

            // 3. Map Telegram IDs to Internal DB Entities
            // Note: Using Set<T>() because explicit DbSets were removed for dynamic registration
            var senderEntity = await db.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == originalSenderId, cancellationToken);
            var targetEntity = await db.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == targetTelegramId, cancellationToken);
            var groupEntity = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == callbackQuery.Message!.Chat.Id, cancellationToken);

            if (senderEntity == null || targetEntity == null || groupEntity == null)
            {
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "系統錯誤: 找不到用戶資料 (可能未同步)。", cancellationToken: cancellationToken);
                return;
            }

            // 4. Process Transaction
            var result = await wpService.ProcessTransactionAsync(
                senderEntity.Id, 
                targetEntity.Id, 
                groupEntity.Id, 
                action == "w", 
                groupEntity.WQuota, 
                groupEntity.PQuota
            );

            // 5. Respond
            if (result.Contains("用完"))
            {
                 // If quota exceeded, show alert but keep buttons
                 await botClient.AnswerCallbackQuery(callbackQuery.Id, result, showAlert: true, cancellationToken: cancellationToken);
            }
            else
            {
                 // Success: Edit the message to show result and remove buttons
                 await botClient.EditMessageText(
                     chatId: callbackQuery.Message.Chat.Id,
                     messageId: callbackQuery.Message.MessageId,
                     text: result,
                     cancellationToken: cancellationToken
                 );
            }
        }
    }
    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram API Error");
        await Task.CompletedTask;
    }
}