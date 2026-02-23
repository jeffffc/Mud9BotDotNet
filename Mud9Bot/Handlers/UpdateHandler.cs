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
    IConfiguration configuration,
    IInlineQueryHandler inlineQueryHandler,
    IBotStatsService botStatsService) : IUpdateHandler
{
    private string? _botUsername;
    private readonly long _logGroupId = configuration.GetValue<long>("BotConfiguration:LogGroupId");
    
    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        // 🚀 1. 流量統計：總數紀錄 (任何進來的更新都先記一筆)
        await botStatsService.RecordUpdateAsync(update, cancellationToken);

        // 🚀 2. 指令預解析與「有效性」驗證統計
        // 這樣做是為了確保：
        // A. /fortune@Mud9Bot 與 /fortune 會合併統計
        // B. 即使指令被後面的 ConversationManager 攔截 return，數據也能先入庫
        // C. 無效指令（如 /asdfg）不會出現在排行榜上
        string? resolvedCommand = null;
        if (update.Message?.Text is { } text && text.StartsWith('/'))
        {
            var parts = text.Split(' ', 2);
            var rawCmd = parts[0].Substring(1);
            int atIndex = rawCmd.IndexOf('@');
            
            // 統一轉為小寫並去掉 Bot Name 尾綴
            string cleanCommand = (atIndex > 0 ? rawCmd.Substring(0, atIndex) : rawCmd).ToLower();
            
            bool isForMe = true;
            if (atIndex > 0)
            {
                var targetBot = rawCmd.Substring(atIndex + 1);
                if (string.IsNullOrEmpty(_botUsername)) _botUsername = (await bot.GetMe(cancellationToken)).Username;
                isForMe = string.Equals(targetBot, _botUsername, StringComparison.OrdinalIgnoreCase);
            }

            if (isForMe)
            {
                // 驗證指令是否為有效註冊的（包含普通指令與對話觸發詞）
                bool isValid = commandRegistry.IsRegistered(cleanCommand) || conversationManager.HasTrigger(cleanCommand);
                
                if (isValid)
                {
                    resolvedCommand = cleanCommand;
                    await botStatsService.RecordEventAsync("command", resolvedCommand, update, cancellationToken);
                }
            }
        }

        // 🚀 3. 按鈕點擊統計 (在 Manager 處理前先紀錄，確保 100% 採集)
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } cb)
        {
            var prefix = cb.Data?.Split('+').FirstOrDefault() ?? "unknown";
            await botStatsService.RecordEventAsync("interaction", $"button_{prefix}", update, cancellationToken);
        }

        // ---------------------------------------------------------
        // 4. 業務邏輯執行 (完全維持您要求的原始順序)
        // ---------------------------------------------------------

        // 4.0. Inline Query Handling
        if (update.Type == UpdateType.InlineQuery && update.InlineQuery is { } inlineQuery)
        {
            await botStatsService.RecordEventAsync("interaction", "inline_query", update, cancellationToken);
            await inlineQueryHandler.HandleAsync(bot, inlineQuery, cancellationToken);
            return;
        }
        
        // 4.0.1. Payment Handling
        if (update.Type == UpdateType.PreCheckoutQuery && update.PreCheckoutQuery is { } preCheckoutQuery)
        {
            await paymentService.HandlePreCheckoutQueryAsync(bot, preCheckoutQuery, cancellationToken);
            return;
        }
        if (update.Message?.SuccessfulPayment is { } successfulPayment)
        {
            await paymentService.HandleSuccessfulPaymentAsync(bot, update.Message, successfulPayment, cancellationToken);
            return;
        }
        
        // 4.1. Conversation Manager (最高優先權業務邏輯)
        if (await conversationManager.HandleUpdateAsync(update, cancellationToken))
        {
            // 如果是對話中的純文字輸入（非指令），補上一筆互動統計
            if (update.Message?.Text != null && !update.Message.Text.StartsWith("/"))
            {
                 await botStatsService.RecordEventAsync("interaction", "conversation_input", update, cancellationToken);
            }
            return; 
        }
        
        // 4.2. Standard Callback Queries (Fallback)
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } callbackQuery)
        {
            using var cbScope = scopeFactory.CreateScope();
            await callbackRegistry.ExecuteAsync(bot, callbackQuery, cbScope.ServiceProvider, cancellationToken);
            return;
        }
        
        // 4.3. Message Extraction & Preliminary Checks
        if (update.Message is not { } message) return;

        // 4.4. Data Synchronization (User & Group)
        using var scope = scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        if (message.From != null) await userService.SyncUserAsync(message.From, cancellationToken);
        if (message.Chat.Type != ChatType.Private) await userService.SyncGroupAsync(message.Chat, cancellationToken);
        
        // 處理進/退群事件
        if (message.NewChatMembers?.Any() == true)
        {
            var welcomeService = scope.ServiceProvider.GetRequiredService<IWelcomeService>();
            await welcomeService.HandleNewChatMembersAsync(bot, message, cancellationToken);
            return;
        }
        if (message.LeftChatMember != null)
        {
            var welcomeService = scope.ServiceProvider.GetRequiredService<IWelcomeService>();
            await welcomeService.HandleLeftChatMemberAsync(bot, message, cancellationToken);
            return;
        }
        
        if (message.Text is not { } messageText) return;
        
        // 4.5. Text Triggers (Regex / Passive Listeners)
        // MessageRegistry 內部已實作 RecordEvent 邏輯
        await messageRegistry.ExecuteAsync(bot, message, scope.ServiceProvider, cancellationToken);

        // 4.6. Command Execution
        // 統計已在 Step 2 完成，此處僅負責執行邏輯
        if (resolvedCommand != null) 
        {
            var finalParts = messageText.Split(' ', 2); 
            var args = finalParts.Length > 1 ? finalParts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>(); 

            logger.LogInformation("Command executed: {Command}", resolvedCommand);
            await commandRegistry.ExecuteAsync(resolvedCommand, args, bot, message, scope.ServiceProvider, cancellationToken);
        }
    }

    public async Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Telegram API Error");
        await Task.CompletedTask;
    }
}