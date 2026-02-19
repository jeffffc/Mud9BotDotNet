using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;

namespace Mud9Bot.Modules;

public class ZodiacModule(IZodiacService zodiacService, IServiceScopeFactory scopeFactory)
{
    private static readonly string[] ZodiacNames = ["白羊", "金牛", "雙子", "巨蟹", "獅子", "處女", "天秤", "天蠍", "人馬", "山羊", "水瓶", "雙魚"];
    private static readonly Dictionary<string, string> TypeLabels = new()
    {
        { "overall", "整體運勢" },
        { "love", "愛情運勢" },
        { "career", "事業運勢" },
        { "money", "財運運勢" }
    };

    private static readonly string[] ErrorMessages = 
    [
        "縮手啦，唔係你嘅野就唔好掂。",
        "撳撳撳……就算你撳爛個 Moon 我都唔會俾你睇人地啲嘢！",
        "撳乜鳩啊？好好玩啊依家？唔係你嘅野就咪搞啦！",
        "係唔俾你睇人地啲嘢呀吹咩？😗"
    ];

    [Command("zodiac")]
    public async Task ZodiacCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // Respect group settings
        if (message.Chat.Type != ChatType.Private)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == message.Chat.Id, ct);
            if (group != null && group.OffZodiac) return;
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: "<b>【今日星座運程】</b>\n你想睇邊個星座呀？",
            parseMode: ParseMode.Html,
            replyMarkup: GetMainKeyboard(),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }

    [CallbackQuery("ZODIAC_MAIN")]
    public async Task HandleMainSelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;

        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: "<b>【今日星座運程】</b>\n你想睇邊個星座呀？",
                parseMode: ParseMode.Html,
                replyMarkup: GetMainKeyboard(),
                cancellationToken: ct
            );
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("is not modified"))
        {
            await bot.AnswerCallbackQuery(query.Id, "你已經喺主選單啦！", cancellationToken: ct);
        }
    }

    [CallbackQuery("ZODIAC")]
    public async Task HandleZodiacSelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var parts = query.Data!.Split('+');
        if (parts.Length < 2 || !int.TryParse(parts[1], out int index)) return;

        if (!await IsOwner(bot, query, ct)) return;

        string text = zodiacService.GetSummary(index);
        
        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: GetSummaryKeyboard(index),
                cancellationToken: ct
            );
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("is not modified"))
        {
            await bot.AnswerCallbackQuery(query.Id, "你咪睇緊呢個囉，揀過個啦！", showAlert: true, cancellationToken: ct);
        }
    }

    [CallbackQuery("TYPEZODIAC")]
    public async Task HandleTypeSelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var parts = query.Data!.Split('+');
        if (parts.Length < 3 || !int.TryParse(parts[1], out int index)) return;
        string type = parts[2];

        if (!await IsOwner(bot, query, ct)) return;

        var fortune = zodiacService.GetDetail(index, type);
        string label = TypeLabels.GetValueOrDefault(type, "運勢");
        string scoreStr = fortune.Score > 0 ? $" ({fortune.Score}/5)" : "";
        string text = $"<b>【{ZodiacNames[index]}座 - {label}{scoreStr}】</b>\n\n{fortune.Text}";

        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: GetDetailKeyboard(index, type),
                cancellationToken: ct
            );
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("is not modified"))
        {
            await bot.AnswerCallbackQuery(query.Id, "你咪睇緊呢個囉，揀過個啦！", showAlert: true, cancellationToken: ct);
        }
    }

    private async Task<bool> IsOwner(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (query.Message?.Chat.Type == ChatType.Private) return true;
        var ownerId = query.Message?.ReplyToMessage?.From?.Id;
        
        if (ownerId.HasValue && query.From.Id != ownerId.Value)
        {
            var random = new Random();
            var errMsg = ErrorMessages[random.Next(ErrorMessages.Length)];
            await bot.AnswerCallbackQuery(query.Id, errMsg, showAlert: true, cancellationToken: ct);
            return false;
        }
        return true;
    }

    // --- Keyboard Generators ---

    private InlineKeyboardMarkup GetMainKeyboard()
    {
        var buttons = ZodiacNames.Select((name, i) => InlineKeyboardButton.WithCallbackData(name, $"ZODIAC+{i}"));
        return new InlineKeyboardMarkup(buttons.Chunk(4));
    }

    private InlineKeyboardMarkup GetSummaryKeyboard(int index)
    {
        var categoryButtons = TypeLabels.Select(kvp => 
            InlineKeyboardButton.WithCallbackData(kvp.Value, $"TYPEZODIAC+{index}+{kvp.Key}")
        ).Chunk(4).ToList();

        // Add back button returning to the Main Zodiac List
        categoryButtons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 返回星座列表", "ZODIAC_MAIN") });

        return new InlineKeyboardMarkup(categoryButtons);
    }

    private InlineKeyboardMarkup GetDetailKeyboard(int index, string activeType)
    {
        var categoryButtons = TypeLabels.Select(kvp => {
            string label = kvp.Key == activeType ? $"📍 {kvp.Value}" : kvp.Value;
            return InlineKeyboardButton.WithCallbackData(label, $"TYPEZODIAC+{index}+{kvp.Key}");
        }).Chunk(4).ToList();

        // Add back button returning to the Specific Zodiac's Summary
        categoryButtons.Add(new[] { InlineKeyboardButton.WithCallbackData($"🔙 返回{ZodiacNames[index]}座", $"ZODIAC+{index}") });

        return new InlineKeyboardMarkup(categoryButtons);
    }
}