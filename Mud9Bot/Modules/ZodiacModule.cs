using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
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

    [Command("zodiac")]
    public async Task ZodiacCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. Group Setting Check
        if (message.Chat.Type != ChatType.Private)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == message.Chat.Id, ct);
            if (group != null && group.OffZodiac) return;
        }

        // 2. Build 4-column sign menu
        var buttons = ZodiacNames.Select((name, i) => InlineKeyboardButton.WithCallbackData(name, $"ZODIAC+{i}"));
        var keyboard = new InlineKeyboardMarkup(buttons.Chunk(4));

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: "<b>【今日星座運程】</b>\n你想睇邊個星座呀？",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }

    [CallbackQuery("ZODIAC")]
    public async Task HandleZodiacSelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var parts = query.Data!.Split('+');
        if (parts.Length < 2 || !int.TryParse(parts[1], out int index)) return;

        // Permission Check (Ensures only the person who called the command can click)
        if (!await IsOwner(bot, query, ct)) return;

        string text = zodiacService.GetSummary(index);
        await bot.EditMessageText(
            chatId: query.Message!.Chat.Id,
            messageId: query.Message.MessageId,
            text: text,
            parseMode: ParseMode.Html,
            replyMarkup: GetCategoryKeyboard(index),
            cancellationToken: ct
        );
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
                replyMarkup: GetCategoryKeyboard(index),
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
            // Uses your existing Cantonese random error list
            await bot.AnswerCallbackQuery(query.Id, Constants.NoOriginalSenderMessageList.GetAny(), showAlert: true, cancellationToken: ct);
            return false;
        }
        return true;
    }

    private InlineKeyboardMarkup GetCategoryKeyboard(int index)
    {
        var buttons = TypeLabels.Select(kvp => 
            InlineKeyboardButton.WithCallbackData(kvp.Value, $"TYPEZODIAC+{index}+{kvp.Key}")
        );
        return new InlineKeyboardMarkup(buttons.Chunk(4));
    }
}