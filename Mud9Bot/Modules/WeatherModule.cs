using Mud9Bot.Attributes;
using Mud9Bot.Services;
using Mud9Bot.Data;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text;
using Telegram.Bot.Exceptions;

namespace Mud9Bot.Modules;

public class WeatherModule(IWeatherService weatherService)
{
    [Command("weather", "w")]
    public async Task WeatherCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var data = weatherService.GetCurrent();
        if (data == null)
        {
            await bot.SendMessage(message.Chat.Id, "暫時未有天氣資料，等我收下風先。", cancellationToken: ct);
            return;
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: GetSummaryText(data),
            parseMode: ParseMode.Html,
            replyMarkup: GetSummaryKeyboard(),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: ct
        );
    }

    [CallbackQuery("WEATHER_SUMM")]
    public async Task HandleSummaryCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var data = weatherService.GetCurrent();
        if (data == null) return;

        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: GetSummaryText(data),
                parseMode: ParseMode.Html,
                replyMarkup: GetSummaryKeyboard(),
                cancellationToken: ct
            );
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified")) { }
    }

    [CallbackQuery("WEATHER_LIST")]
    public async Task HandleListCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var data = weatherService.GetCurrent();
        if (data == null) return;

        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: "<b>你想睇邊個區嘅天氣？</b>",
                parseMode: ParseMode.Html,
                replyMarkup: GetDistrictListKeyboard(data),
                cancellationToken: ct
            );
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified")) { }
    }

    [CallbackQuery("DISTRICTWEATHER")]
    public async Task HandleDistrictWeatherCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var parts = query.Data!.Split('+');
        if (parts.Length < 4) return;

        string time = parts[1];
        string name = parts[2];
        string temp = parts[3];

        var sb = new StringBuilder();
        sb.AppendLine($"更新時間︰{time}");
        sb.AppendLine($"<b>【{name}】</b> 嘅溫度係: <code>{temp}</code> 度");

        var backButton = InlineKeyboardButton.WithCallbackData("🔙 返回地區列表", "WEATHER_LIST");

        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: sb.ToString(),
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(backButton),
                cancellationToken: ct
            );
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("not modified")) { }
    }

    // --- 輔助方法 ---

    private string GetSummaryText(Mud9Bot.Models.WeatherData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>【本港現時天氣】</b>");
        sb.AppendLine($"🌡 氣溫：{data.CurrentTemp}℃");
        sb.AppendLine($"💧 濕度：{data.Humidity}%");
        sb.AppendLine($"🕒 更新：{data.UpdateTime}");
        return sb.ToString();
    }

    private InlineKeyboardMarkup GetSummaryKeyboard()
    {
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("睇各區氣溫 🗺️", "WEATHER_LIST"));
    }

    private InlineKeyboardMarkup GetDistrictListKeyboard(Mud9Bot.Models.WeatherData data)
    {
        var buttons = new List<InlineKeyboardButton>();
        foreach (var district in data.Districts)
        {
            string displayName = district.Name.Replace("&#40050;", "鱲");
            string callbackData = $"DISTRICTWEATHER+{data.UpdateTime}+{displayName}+{district.Temperature}";
            buttons.Add(InlineKeyboardButton.WithCallbackData(displayName, callbackData));
        }

        var rows = buttons.Chunk(3).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 返回總覽", "WEATHER_SUMM") });
        return new InlineKeyboardMarkup(rows);
    }

    private async Task<bool> IsOwner(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (query.Message?.Chat.Type == ChatType.Private) return true;
        var originalSenderId = query.Message?.ReplyToMessage?.From?.Id;
        if (originalSenderId.HasValue && query.From.Id != originalSenderId.Value)
        {
            await bot.AnswerCallbackQuery(query.Id, Constants.NoOriginalSenderMessageList.GetAny(), showAlert: true, cancellationToken: ct);
            return false;
        }
        return true;
    }
}