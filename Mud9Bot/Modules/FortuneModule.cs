using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;

namespace Mud9Bot.Modules;

public class FortuneModule(IFortuneService fortuneService, IServiceScopeFactory scopeFactory)
{
    [Command("fortune")]
    public async Task Fortune(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var chatTelegramId = message.Chat.Id;
        var userTelegramId = message.From?.Id ?? 0;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // 1. Check Group Settings & Daily Limit (only for non-private chats)
        if (message.Chat.Type != ChatType.Private)
        {
            // Fetch the group and user entities to get internal IDs
            var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == chatTelegramId, ct);
            var user = await db.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == userTelegramId, ct);

            if (group == null || group.OffFortune || user == null) return;

            // Current date in Hong Kong
            var todayHk = DateTime.UtcNow.ToHkTime().Date;

            // Check if user already requested a fortune today in this group
            var limit = await db.Set<FortuneLimit>()
                .FirstOrDefaultAsync(l => l.UserId == user.Id && l.GroupId == group.Id, ct);

            // Compare the date component after adjusting for HK offset
            if (limit != null && limit.LastDate.ToHkTime().Date == todayHk)
            {
                string limitMsg = "你今日咪喺度求過籤囉，求得多好嘅唔靈醜嘅靈㗎！";
                try
                {
                    await bot.SendMessage(
                        chatId: chatTelegramId,
                        text: limitMsg,
                        replyParameters: new ReplyParameters { MessageId = limit.MessageId },
                        cancellationToken: ct
                    );
                }
                catch
                {
                    await bot.SendMessage(chatTelegramId, "你今日咪喺度求過籤囉！", cancellationToken: ct);
                }
                return;
            }
        }

        // 2. Generate Fortune
        var (fortune, index) = fortuneService.GetRandomFortune();

        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("解籤", $"fortune+{index}")
        );

        var sentMsg = await bot.SendMessage(
            chatId: message.Chat.Id,
            text: fortune.a,
            replyMarkup: keyboard,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );

        // 3. Update/Insert Limit Record
        if (message.Chat.Type != ChatType.Private)
        {
            var group = await db.Set<BotGroup>().FirstOrDefaultAsync(g => g.TelegramId == chatTelegramId, ct);
            var user = await db.Set<BotUser>().FirstOrDefaultAsync(u => u.TelegramId == userTelegramId, ct);

            if (group != null && user != null)
            {
                var existingLimit = await db.Set<FortuneLimit>()
                    .FirstOrDefaultAsync(l => l.UserId == user.Id && l.GroupId == group.Id, ct);

                if (existingLimit == null)
                {
                    db.Set<FortuneLimit>().Add(new FortuneLimit
                    {
                        UserId = user.Id,
                        GroupId = group.Id,
                        LastDate = DateTime.UtcNow, // Explicitly use UTC for Postgres compatibility
                        MessageId = sentMsg.MessageId
                    });
                }
                else
                {
                    existingLimit.LastDate = DateTime.UtcNow;
                    existingLimit.MessageId = sentMsg.MessageId;
                }

                await db.SaveChangesAsync(ct);
            }
        }
    }

    [CallbackQuery("fortune")]
    public async Task HandleFortuneCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var data = query.Data ?? "";
        var parts = data.Split('+');

        if (parts.Length < 2 || !int.TryParse(parts[1], out int index))
        {
            await bot.AnswerCallbackQuery(query.Id, "找不到靈籤資料。", cancellationToken: ct);
            return;
        }

        if (query.Message?.Chat.Type != ChatType.Private)
        {
            var originalSenderId = query.Message?.ReplyToMessage?.From?.Id;
            if (originalSenderId.HasValue && query.From.Id != originalSenderId.Value)
            {
                var random = new Random();
                var errmsg = Constants.NoOriginalSenderMessageList[random.Next(Constants.NoOriginalSenderMessageList.Length)];
                await bot.AnswerCallbackQuery(query.Id, errmsg, showAlert: true, cancellationToken: ct);
                return;
            }
        }

        var fortune = fortuneService.GetFortuneByIndex(index);
        if (fortune == null)
        {
            await bot.AnswerCallbackQuery(query.Id, "找不到靈籤資料。", cancellationToken: ct);
            return;
        }

        var explanation = fortune.b;

        if (explanation.Length <= 200)
        {
            try
            {
                await bot.AnswerCallbackQuery(query.Id, explanation, showAlert: true, cancellationToken: ct);
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("message is too long"))
            {
                await RedirectToPrivate(bot, query, explanation, ct);
            }
        }
        else
        {
            await RedirectToPrivate(bot, query, explanation, ct);
        }
    }

    private async Task RedirectToPrivate(ITelegramBotClient bot, CallbackQuery query, string text, CancellationToken ct)
    {
        try
        {
            await bot.SendMessage(query.From.Id, text, parseMode: ParseMode.Html, cancellationToken: ct);
            await bot.AnswerCallbackQuery(query.Id, "Sorly 個解籤太長，我決定私底下俾你睇！", showAlert: true, cancellationToken: ct);
        }
        catch (ApiRequestException)
        {
            var me = await bot.GetMe(ct);
            await bot.AnswerCallbackQuery(query.Id, 
                $"Sorly 個解籤太長，我決定私底下俾你睇！但你好似未啟動我，不如你去 @{me.Username} 撳個 Start 制先再番黎。", 
                showAlert: true, 
                cancellationToken: ct);
        }
    }
}