using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Mud9Bot.Modules;

public class MovieModule(IMovieService movieService, ILogger<MovieModule> logger)
{
    [Command("movies")]
    [TextTrigger("有咩戲睇",  Description = "取得 WMOOV 即日上映電影")]
    public async Task MoviesCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var movies = movieService.GetCachedMovies();

        if (!movies.Any())
        {
            await bot.SendMessage(message.Chat.Id, "暫時冇電影資訊，等我更新下先。", cancellationToken: ct);
            return;
        }

        var text = new StringBuilder("<b>現在上映（撳制查詢詳情）</b>\n");
        var buttons = new List<InlineKeyboardButton>();

        for (int i = 0; i < movies.Count; i++)
        {
            var movie = movies[i];
            text.AppendLine($"<b>{i + 1}</b>. {movie.Title} (<b>{movie.Rating}</b> 分)");
            
            // 更新按鈕文字格式：#1 電影名稱
            string buttonLabel = $"#{i + 1} {movie.Title}";
            buttons.Add(InlineKeyboardButton.WithCallbackData(buttonLabel, $"MOVIES+{movie.Id}"));
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: text.ToString(),
            parseMode: ParseMode.Html,
            // 考慮到按鈕文字變長，將原本的一排 3 個改為一排 2 個，以確保文字能完整顯示
            replyMarkup: new InlineKeyboardMarkup(buttons.Chunk(2)),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }

    [CallbackQuery("MOVIES")]
    public async Task HandleMovieCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var parts = query.Data!.Split('+');
        if (parts.Length < 2 || !int.TryParse(parts[1], out int movieId)) return;

        if (query.Message?.Chat.Type != ChatType.Private)
        {
            var originalSenderId = query.Message?.ReplyToMessage?.From?.Id;
            if (originalSenderId.HasValue && query.From.Id != originalSenderId.Value)
            {
                await bot.AnswerCallbackQuery(query.Id, Constants.NoOriginalSenderMessageList.GetAny(), showAlert: true, cancellationToken: ct);
                return;
            }
        }

        var movies = movieService.GetCachedMovies();
        var movie = movies.FirstOrDefault(m => m.Id == movieId);
        
        if (movie == null)
        {
            await bot.AnswerCallbackQuery(query.Id, "搵唔到呢套戲嘅資料，可能已經落左畫。", showAlert: true, cancellationToken: ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"<a href='{movie.Link}'><b>{movie.Title}</b></a> ({movie.Rating} 分)");
        
        if (!string.IsNullOrWhiteSpace(movie.Description)) sb.AppendLine($"\n簡介︰{movie.Description}");
        if (!string.IsNullOrWhiteSpace(movie.Genre)) sb.AppendLine($"片種︰{movie.Genre}");
        if (!string.IsNullOrWhiteSpace(movie.Director)) sb.AppendLine($"導演︰{movie.Director}");
        if (!string.IsNullOrWhiteSpace(movie.Starring)) sb.AppendLine($"主演︰{movie.Starring}");
        if (!string.IsNullOrWhiteSpace(movie.Length)) sb.AppendLine($"片長︰{movie.Length}");
        if (!string.IsNullOrWhiteSpace(movie.Grade)) sb.AppendLine($"級別︰{movie.Grade}");
        if (!string.IsNullOrWhiteSpace(movie.Language)) sb.AppendLine($"語言︰{movie.Language}");
        if (!string.IsNullOrWhiteSpace(movie.OnShowDate)) sb.AppendLine($"上映︰{movie.OnShowDate}");

        string detailText = sb.ToString();

        if (detailText.Length > 4000) detailText = detailText.Substring(0, 3900) + "...";

        // 同步更新導航按鈕的格式
        var navButtons = movies
            .Select((m, i) => InlineKeyboardButton.WithCallbackData($"#{i + 1} {m.Title}", $"MOVIES+{m.Id}"))
            .Chunk(2);

        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: detailText,
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                replyMarkup: new InlineKeyboardMarkup(navButtons),
                cancellationToken: ct
            );
        }
        catch (Exception ex) when (ex.Message.Contains("is not modified"))
        {
            await bot.AnswerCallbackQuery(query.Id, "你咪睇緊呢個囉，揀過個啦！", showAlert: true, cancellationToken: ct);
        }
    }
}