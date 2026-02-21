using System.Text;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Mud9Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;

namespace Mud9Bot.Modules;

public class NewsModule(INewsService newsService)
{
    private static readonly Dictionary<NewsCategory, string> CategoryNames = new()
    {
        { NewsCategory.Local, "本地" },
        { NewsCategory.GreaterChina, "大中華" },
        { NewsCategory.International, "國際" },
        { NewsCategory.Finance, "財經" },
        { NewsCategory.Sports, "體育" }
    };

    [Command("news", Description = "查看即時新聞短打")]
    [TextTrigger("有咩新聞", Description = "查詢最新新聞")]
    public async Task NewsCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: "<b>【新聞短打】</b>\n你想睇邊類新聞？",
            parseMode: ParseMode.Html,
            replyMarkup: GetCategoryKeyboard(),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }

    [CallbackQuery("NEWS_CAT")]
    public async Task HandleCategorySelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        
        await bot.EditMessageText(
            chatId: query.Message!.Chat.Id,
            messageId: query.Message.MessageId,
            text: "<b>【新聞短打】</b>\n你想睇邊類新聞？",
            parseMode: ParseMode.Html,
            replyMarkup: GetCategoryKeyboard(),
            cancellationToken: ct
        );
    }

    [CallbackQuery("NEWS_LIST")]
    public async Task HandleListSelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        
        var parts = query.Data!.Split('+');
        if (parts.Length < 2 || !Enum.TryParse<NewsCategory>(parts[1], out var cat)) return;

        var news = newsService.GetNews(cat);
        if (!news.Any())
        {
            await bot.AnswerCallbackQuery(query.Id, "暫時未有呢類新聞嘅資料。", showAlert: true, cancellationToken: ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<b>【即時{CategoryNames[cat]}新聞】</b>\n");
        for (int i = 0; i < news.Count; i++)
        {
            sb.AppendLine($"<b>{i + 1}.</b> {news[i].Title.EscapeHtml()}");
        }

        await bot.EditMessageText(
            chatId: query.Message!.Chat.Id,
            messageId: query.Message.MessageId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: GetNewsListKeyboard(cat, news),
            cancellationToken: ct
        );
    }

    [CallbackQuery("NEWS_DETAIL")]
    public async Task HandleDetailSelect(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;

        var parts = query.Data!.Split('+');
        if (parts.Length < 3 || !Enum.TryParse<NewsCategory>(parts[1], out var cat) || !int.TryParse(parts[2], out int index)) return;

        var news = newsService.GetNews(cat);
        if (index < 0 || index >= news.Count) return;

        var article = news[index];
        var sb = new StringBuilder();
        sb.AppendLine($"<b>【{CategoryNames[cat]}】{article.Title.EscapeHtml()}</b>\n");
        sb.AppendLine(article.Description.EscapeHtml());
        sb.AppendLine($"\n<a href='{article.Link}'>🔗 閱讀全文</a>");

        await bot.EditMessageText(
            chatId: query.Message!.Chat.Id,
            messageId: query.Message.MessageId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            replyMarkup: GetDetailKeyboard(cat),
            cancellationToken: ct
        );
    }

    private InlineKeyboardMarkup GetCategoryKeyboard()
    {
        var buttons = CategoryNames.Select(kvp => 
            InlineKeyboardButton.WithCallbackData(kvp.Value, $"NEWS_LIST+{kvp.Key}")
        ).Chunk(3);
        return new InlineKeyboardMarkup(buttons);
    }

    private InlineKeyboardMarkup GetNewsListKeyboard(NewsCategory cat, List<NewsArticle> news)
    {
        var buttons = news.Select((a, i) => 
            InlineKeyboardButton.WithCallbackData((i + 1).ToString(), $"NEWS_DETAIL+{cat}+{i}")
        ).Chunk(5).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 返回分類", "NEWS_CAT") });
        return new InlineKeyboardMarkup(buttons);
    }

    private InlineKeyboardMarkup GetDetailKeyboard(NewsCategory cat)
    {
        return new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 返回列表", $"NEWS_LIST+{cat}"),
            InlineKeyboardButton.WithCallbackData("🏠 主目錄", "NEWS_CAT")
        });
    }

    private async Task<bool> IsOwner(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (query.Message?.Chat.Type == ChatType.Private) return true;
        var ownerId = query.Message?.ReplyToMessage?.From?.Id;
        if (ownerId.HasValue && query.From.Id != ownerId.Value)
        {
            await bot.AnswerCallbackQuery(query.Id, Constants.NoOriginalSenderMessageList.GetAny(), showAlert: true, cancellationToken: ct);
            return false;
        }
        return true;
    }
}