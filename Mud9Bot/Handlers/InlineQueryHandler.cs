using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Mud9Bot.Interfaces;
using Mud9Bot.Models;
using Mud9Bot.Extensions;
using System.Linq;
using System.Text;

namespace Mud9Bot.Handlers;

public class InlineQueryHandler(
    IWeatherService weatherService,
    ITrafficService trafficService,
    IMarkSixService markSixService,
    INewsService newsService) : IInlineQueryHandler
{
    public async Task HandleAsync(ITelegramBotClient bot, InlineQuery query, CancellationToken ct)
    {
        var results = new List<InlineQueryResult>();

        // 1. Add Fixed Information Cards

        // Traffic - RTHK Traffic News (Converting legacy backticks to code tags)
        string trafficNews = await trafficService.GetTrafficNewsAsync(ct);
        var trafficHtml = ConvertMarkdownToHtml(trafficNews);
        results.Add(new InlineQueryResultArticle(
            id: "traffic",
            title: "🚦 交通消息",
            inputMessageContent: new InputTextMessageContent($"<b>最新交通:</b>\n{trafficHtml}") { ParseMode = ParseMode.Html }
        )
        {
            Description = "最新 RTHK 即時交通快訊"
        });

        // Weather
        var weather = weatherService.GetCurrent();
        if (weather != null)
        {
            string weatherMsg = $"<b>最新天氣:</b>\n🌡 氣溫：<code>{weather.CurrentTemp}℃</code>\n💧 濕度：<code>{weather.Humidity}%</code>\n🕒 更新：<code>{weather.UpdateTime}</code>";
            results.Add(new InlineQueryResultArticle(
                id: "weather",
                title: "☁️ 天氣資訊",
                inputMessageContent: new InputTextMessageContent(weatherMsg) { ParseMode = ParseMode.Html }
            )
            {
                Description = "本港現時天氣概況"
            });
        }

        // Mark Six
        var marksix = markSixService.GetLatestResult();
        if (marksix != null)
        {
            string msMsg = $"<b>🎰 最新攪珠結果:</b>\n期數: <code>{marksix.Period.EscapeHtml()}</code>\n號碼: <b>{string.Join(", ", marksix.Numbers)}</b>\n特別號碼: <b>{marksix.SpecialBall}</b> 🔴";
            results.Add(new InlineQueryResultArticle(
                id: "marksix",
                title: "🎰 六合彩結果",
                inputMessageContent: new InputTextMessageContent(msMsg) { ParseMode = ParseMode.Html }
            )
            {
                Description = "最近一期開獎號碼及期數"
            });
        }

        // News (Local, International, Sports)
        AddNewsResult(results, NewsCategory.Local, "本地", "local");
        AddNewsResult(results, NewsCategory.International, "國際", "intl");
        AddNewsResult(results, NewsCategory.Sports, "體育", "sports");

        // 2. Send results back to Telegram
        await bot.AnswerInlineQuery(query.Id, results, cacheTime: 60, cancellationToken: ct);
    }

    private void AddNewsResult(List<InlineQueryResult> results, NewsCategory cat, string label, string prefixId)
    {
        var articles = newsService.GetNews(cat);
        if (articles.Any())
        {
            var top = articles.First();
            // Using HTML <a> tag for links
            string newsMsg = $"<b>📰 {label}新聞:</b>\n<a href='{top.Link}'>{top.Title.EscapeHtml()}</a>\n\n{top.Description.EscapeHtml()}";
            
            results.Add(new InlineQueryResultArticle(
                id: $"{prefixId}_news",
                title: $"📰 {label}新聞",
                inputMessageContent: new InputTextMessageContent(newsMsg) { ParseMode = ParseMode.Html }
            )
            {
                Description = top.Title
            });
        }
    }

    /// <summary>
    /// Helper to convert simple Markdown backticks `text` to <code>text</code> for HTML parse mode.
    /// </summary>
    private string ConvertMarkdownToHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var parts = input.Split('`');
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1) // Inside backticks
            {
                sb.Append($"<code>{parts[i].EscapeHtml()}</code>");
            }
            else
            {
                sb.Append(parts[i].EscapeHtml());
            }
        }
        return sb.ToString();
    }
}