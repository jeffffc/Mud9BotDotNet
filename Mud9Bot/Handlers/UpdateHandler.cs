using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Mud9Bot.Registries;
using Mud9Bot.Interfaces; 
using Mud9Bot.Modules.Conversations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Mud9Bot.Handlers;

public class UpdateHandler(
    ILogger<UpdateHandler> logger, 
    CommandRegistry commandRegistry,
    CallbackQueryRegistry callbackRegistry,
    MessageRegistry messageRegistry,
    IServiceScopeFactory scopeFactory,
    ConversationManager conversationManager,
    IPaymentService paymentService,
    IConfiguration configuration) : IUpdateHandler
{
    private string? _botUsername;
    private readonly long _logGroupId = configuration.GetValue<long>("BotConfiguration:LogGroupId");
    
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // ---------------------------------------------------------
        // 0. Payment Handling (Highest Priority)
        // ---------------------------------------------------------
        // 處理支付相關的 API 請求，避免被後續邏輯誤擋
        if (update.Type == UpdateType.PreCheckoutQuery && update.PreCheckoutQuery is { } preCheckoutQuery)
        {
            await paymentService.HandlePreCheckoutQueryAsync(botClient, preCheckoutQuery, cancellationToken);
            return;
        }
        
        if (update.Message?.SuccessfulPayment is { } successfulPayment)
        {
            await paymentService.HandleSuccessfulPaymentAsync(botClient, update.Message, successfulPayment, cancellationToken);
            return;
        }
        
        // ---------------------------------------------------------
        // 1. Conversation Manager (Active Sessions & Entry Points)
        // ---------------------------------------------------------
        // 優先處理使用者是否正在進行中的對話 (例如 Settings)，或者是否觸發了對話的進入點
        if (await conversationManager.HandleUpdateAsync(update, cancellationToken))
        {
            return; 
        }
        
        // ---------------------------------------------------------
        // 2. Standard Callback Queries (Fallback)
        // ---------------------------------------------------------
        // 如果按鈕事件不屬於任何對話流程，則交給這裡處理
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } callbackQuery)
        {
            using var cbScope = scopeFactory.CreateScope();
            await callbackRegistry.ExecuteAsync(botClient, callbackQuery, cbScope.ServiceProvider, cancellationToken);
            return;
        }
        
        // ---------------------------------------------------------
        // 3. Message Extraction & Preliminary Checks
        // ---------------------------------------------------------
        // 確保這是一個包含文字的普通訊息
        if (update.Message is not { } message) return;
        if (message.Text is not { } messageText) return;

        var chatId = message.Chat.Id;

        // ---------------------------------------------------------
        // 4. Data Synchronization (User & Group)
        // ---------------------------------------------------------
        // 在執行任何進階操作前，確保資料庫有最新的用戶與群組資料
        using var scope = scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        
        if (message.From != null)
        {
            await userService.SyncUserAsync(message.From, cancellationToken);
        }
        if (message.Chat.Type != ChatType.Private)
        {
            await userService.SyncGroupAsync(message.Chat, cancellationToken);
        }
        
        // ---------------------------------------------------------
        // 5. Text Triggers (Regex / Passive Listeners)
        // ---------------------------------------------------------
        // 被動監聽所有文字訊息 (例如: 早晨、晚安)。
        // 必須放在 `StartsWith('/')` 之前執行，否則普通文字會被拋棄。
        await messageRegistry.ExecuteAsync(botClient, message, scope.ServiceProvider, cancellationToken);

        // ---------------------------------------------------------
        // 6. Command Execution
        // ---------------------------------------------------------
        // 如果訊息不是以 '/' 開頭，到此為止 (只記錄 LastSeen，不當作指令處理)
        if (!messageText.StartsWith('/')) return; 
        
        // --- PARSING LOGIC START ---
        var parts = messageText.Split(' ', 2); 
        var commandPart = parts[0].Substring(1); 
        var args = parts.Length > 1 ? parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>(); 

        // 處理 @BotName 的情況 (例如 /help@Mud9Bot)
        var atIndex = commandPart.IndexOf('@');
        if (atIndex > 0)
        {
            var targetBot = commandPart.Substring(atIndex + 1); 

            if (string.IsNullOrEmpty(_botUsername))
            {
                var me = await botClient.GetMe(cancellationToken);
                _botUsername = me.Username;
            }

            if (!string.Equals(targetBot, _botUsername, StringComparison.OrdinalIgnoreCase))
            {
                return; // 如果指令是給群組內其他 Bot 的，就忽略
            }

            commandPart = commandPart.Substring(0, atIndex); 
        }
        // --- PARSING LOGIC END ---

        logger.LogInformation("Command detected: {CommandPart}", commandPart);

        await commandRegistry.ExecuteAsync(commandPart, args, botClient, message, scope.ServiceProvider, cancellationToken);
    }

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram API Error");
        await Task.CompletedTask;
    }
}