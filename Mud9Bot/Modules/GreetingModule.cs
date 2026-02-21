using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class GreetingModule(IGreetingService greetingService, IServiceScopeFactory scopeFactory, ILogger<GreetingModule> logger)
{
    // Morning Triggers
    [TextTrigger(@"(?i)(hello|早安|早晨)", Description = "Custom morning greetings")]
    public async Task HandleMorningAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        await ProcessGreetingAsync(bot, message, "MORNING", ct);
    }

    // Night Triggers
    [TextTrigger(@"(?i)(good night|晚安|早抖)", Description = "Custom night greetings")]
    public async Task HandleNightAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        await ProcessGreetingAsync(bot, message, "NIGHT", ct);
    }

    private async Task ProcessGreetingAsync(ITelegramBotClient bot, Message message, string greetingType, CancellationToken ct)
    {
        if (message.From == null) return;
        long userId = message.From.Id;

        // Fetch directly from RAM cache. Returns null if user is not in the "selected users" list.
        var greeting = greetingService.GetRandomGreeting(userId, greetingType);

        // 如果找不到該用戶的專屬訊息，直接安靜離開，不產生任何錯誤 Log
        if (string.IsNullOrEmpty(greeting)) return; 

        // 只有在確認要發送，但 Telegram API 執行失敗時，才會記錄 Log
        try
        {
            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: greeting,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {GreetingType} greeting to user {UserId}. Content: {Content}", greetingType, userId, greeting);
        }
    }

    // ---------------------------------------------------------
    // QUICK ADD ACTIONS (Dev Only)
    // ---------------------------------------------------------

    [Command("addmorning", DevOnly = true, Description = "快速新增早晨訊息 (需回覆目標訊息)")]
    public async Task AddMorningCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await QuickAddAsync(bot, message, args, "MORNING", ct);
    }

    [Command("addnight", DevOnly = true, Description = "快速新增晚安訊息 (需回覆目標訊息)")]
    public async Task AddNightCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await QuickAddAsync(bot, message, args, "NIGHT", ct);
    }

    private async Task QuickAddAsync(ITelegramBotClient bot, Message message, string[] args, string type, CancellationToken ct)
    {
        if (message.ReplyToMessage == null)
        {
            await bot.Reply(message, $"請對住一條別人嘅訊息 Reply：<code>/add{type.ToLower()} [問候語內容]</code>", ct: ct);
            return;
        }

        if (args.Length == 0)
        {
            await bot.Reply(message, $"請輸入要新增嘅問候語內容，例如：<code>/add{type.ToLower()} 早晨呀！</code>", ct: ct);
            return;
        }

        long targetUserId = message.ReplyToMessage.From!.Id;
        string content = string.Join(" ", args);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // 防呆：防止重複加入一樣的句子
        bool exists = await db.Set<CustomGreeting>().AnyAsync(g => g.TelegramId == targetUserId && g.GreetingType == type && g.Content == content, ct);
        if (exists)
        {
            await bot.Reply(message, "呢句問候語已經存在啦！", ct: ct);
            return;
        }

        db.Set<CustomGreeting>().Add(new CustomGreeting
        {
            TelegramId = targetUserId,
            GreetingType = type,
            Content = content
        });

        await db.SaveChangesAsync(ct);
        
        // Instant RAM Refresh!
        await greetingService.InitializeAsync();

        string typeName = type == "MORNING" ? "早晨" : "晚安";
        await bot.Reply(message, $"✅ 成功為用戶 <code>{targetUserId}</code> 綁定專屬{typeName}訊息！", ct: ct);
    }
}