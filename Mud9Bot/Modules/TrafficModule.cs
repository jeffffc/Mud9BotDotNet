using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Services;
using Mud9Bot.Data;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;
using System.Text;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Modules;

public class TrafficModule(ITrafficService trafficService)
{
    // --- 1. RTHK 交通消息 ---
    [Command("traffic", Description = "獲取 RTHK 即時交通消息")]
    [TextTrigger("交通消息", Description = "取得 RTHK 即時交通快訊")] // 🚀 新增 TextTrigger
    public async Task GetTraffic(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        var sentMsg = await bot.Reply(msg, "🔄 正在獲取 RTHK 交通消息...", ct);
        var news = await trafficService.GetTrafficNewsAsync(ct);

        // 使用 Split 邏輯將成對的反引號替換為 HTML <code> 標籤
        // 這比簡單的 Replace 更可靠，能確保標籤成對閉合
        var newsParts = news.Split('`');
        var sbNews = new StringBuilder();
        for (int i = 0; i < newsParts.Length; i++)
        {
            // 奇數索引代表在反引號內部的文字
            sbNews.Append(i % 2 == 1 ? $"<code>{newsParts[i]}</code>" : newsParts[i]);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("🚦 <b>RTHK 交通消息</b>");
        sb.AppendLine();
        sb.AppendLine(sbNews.ToString());

        await bot.EditMessageText(
            chatId: msg.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html, // 統一使用 HTML
            messageId: sentMsg.MessageId,
            cancellationToken: ct);
    }

    // --- 2. 交通快拍功能 ---
    [Command("trafficsnapshot", "snapshot", Description = "查看本港各區交通快拍")]
    public async Task TrafficSnapshotCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (message.Chat.Type != ChatType.Private)
        {
            await bot.SendMessage(message.Chat.Id, "呢度用唔到，要私訊先得。🔒", cancellationToken: ct);
            return;
        }

        var regions = trafficService.GetRegions();
        if (!regions.Any())
        {
            await bot.SendMessage(message.Chat.Id, "暫時未有交通快拍資料，等我更新下先。", cancellationToken: ct);
            return;
        }

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: "<b>【交通快拍】</b>\n你想睇邊個區域？",
            parseMode: ParseMode.Html,
            replyMarkup: GetRegionKeyboard(regions),
            cancellationToken: ct
        );
    }

    [CallbackQuery("TRAFFIC_MAIN")]
    public async Task HandleMain(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var regions = trafficService.GetRegions();
        await EditToText(bot, query, "<b>【交通快拍】</b>\n你想睇邊個區域？", GetRegionKeyboard(regions), ct);
    }

    [CallbackQuery("TRAFFIC_REG")]
    public async Task HandleRegion(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var regionName = query.Data!.Split('+')[1];
        var region = trafficService.GetRegions().FirstOrDefault(r => r.Name == regionName);
        if (region == null) return;

        var buttons = region.Districts.Select(d => 
            InlineKeyboardButton.WithCallbackData(d.Name, $"TRAFFIC_DIST+{regionName}+{d.Name}")
        ).Chunk(2).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 返回主選單", "TRAFFIC_MAIN") });

        await EditToText(bot, query, $"你想睇<b>【{regionName}】</b>嘅邊個分區？", new InlineKeyboardMarkup(buttons), ct);
    }

    [CallbackQuery("TRAFFIC_DIST")]
    public async Task HandleDistrict(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var parts = query.Data!.Split('+');
        var regionName = parts[1];
        var distName = parts[2];

        var region = trafficService.GetRegions().FirstOrDefault(r => r.Name == regionName);
        var district = region?.Districts.FirstOrDefault(d => d.Name == distName);
        if (district == null) return;

        var buttons = district.Cameras.Select(c => 
            InlineKeyboardButton.WithCallbackData(c.Description, $"TRAFFIC_SNAP+{regionName}+{distName}+{c.Id}")
        ).Chunk(1).ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 返回分區列表", $"TRAFFIC_REG+{regionName}") });

        await EditToText(bot, query, $"你想睇<b>【{distName}】</b>邊個快拍站呢？", new InlineKeyboardMarkup(buttons), ct);
    }

    [CallbackQuery("TRAFFIC_SNAP")]
    public async Task HandleSnapshot(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        if (!await IsOwner(bot, query, ct)) return;
        var parts = query.Data!.Split('+');
        var regionName = parts[1];
        var distName = parts[2];
        var cameraId = parts[3];

        var camera = trafficService.GetRegions()
            .SelectMany(r => r.Districts)
            .SelectMany(d => d.Cameras)
            .FirstOrDefault(c => c.Id == cameraId);

        string locationName = camera?.Description ?? "未知位置";
        string photoUrl = $"http://tdcctv.data.one.gov.hk/{cameraId}.JPG";
        
        var backButton = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("🔙 返回攝影機列表", $"TRAFFIC_DIST+{regionName}+{distName}")
        );

        try
        {
            await bot.EditMessageMedia(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                media: new InputMediaPhoto(InputFile.FromUri(photoUrl)) 
                { 
                    Caption = $"📸 <b>位置：{locationName}</b>\n📍 編號：<code>{cameraId}</code>\n🕒 獲取時間：{DateTime.Now:HH:mm:ss}",
                    ParseMode = ParseMode.Html
                },
                replyMarkup: backButton,
                cancellationToken: ct
            );
        }
        catch (Exception)
        {
            await bot.AnswerCallbackQuery(query.Id, "暫時抓取唔到快拍圖，請稍後再試。", showAlert: true, cancellationToken: ct);
        }
    }

    // --- Helpers ---

    private InlineKeyboardMarkup GetRegionKeyboard(List<TrafficRegion> regions)
    {
        var buttons = regions.Select(r => InlineKeyboardButton.WithCallbackData(r.Name, $"TRAFFIC_REG+{r.Name}"));
        return new InlineKeyboardMarkup(buttons.Chunk(2));
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

    private async Task EditToText(ITelegramBotClient bot, CallbackQuery query, string text, InlineKeyboardMarkup? markup, CancellationToken ct)
    {
        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: markup,
                cancellationToken: ct
            );
        }
        catch (ApiRequestException)
        {
            await bot.DeleteMessage(query.Message!.Chat.Id, query.Message.MessageId, ct);
            await bot.SendMessage(
                chatId: query.Message.Chat.Id,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: markup,
                cancellationToken: ct
            );
        }
    }
}