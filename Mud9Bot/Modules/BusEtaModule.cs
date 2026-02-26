using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Mud9Bot.Attributes;

namespace Mud9Bot.Modules;

/// <summary>
/// Module to handle Bus ETA requests via Telegram WebApp.
/// 處理巴士 ETA 請求嘅 WebApp 模組。
/// </summary>
public class BusEtaModule(IConfiguration config, ITelegramBotClient botClient, ILogger<BusEtaModule> logger)
{
    /// <summary>
    /// Handles the /bus command to launch the Mini App.
    /// 處理 /bus 指令，用嚟開個 Mini App 出嚟。
    /// </summary>
    [Command("bus")]
    public async Task HandleBusCommand(Message message)
    {
        // Retrieve the WebApp URL and Log Group ID from configuration
        // 喺 appsettings.json 攞返個 WebAppUrl 同埋 Log Group ID
        var webAppUrl = config["WebApp:WebAppUrl"]; 
        var logGroupId = config["BotConfiguration:LogGroupId"];

        // Validate if the URL exists in configuration
        // 檢查下有無 set 到 URL
        if (string.IsNullOrEmpty(webAppUrl))
        {
            // 1. Reply to user using Mud9Bot's persona
            // 用返 Mud9Bot 嘅語氣覆 user 話用唔到住
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "呢個功能暫時仲未用得住喎，遲啲先啦！🌚"
            );

            // 2. Send an extra message to the log group to alert the admin
            // 靜靜雞話比 Log Group 聽有人撞板，等 admin 知要執嘢
            if (!string.IsNullOrEmpty(logGroupId))
            {
                var userInfo = message.From != null 
                    ? $"@{message.From.Username ?? "N/A"} ({message.From.Id})" 
                    : "Unknown User";

                await botClient.SendMessage(
                    chatId: logGroupId,
                    text: $"⚠️ 報告！有人試圖用 /bus 指令，但係 WebAppUrl 仲未 set 呀！\n\nUser: {userInfo}"
                );
            }

            logger.LogWarning("[BusModule] WebAppUrl is missing in configuration.");
            return;
        }

        // Create a WebApp button linking to the configured URL
        // 整返粒掣，等 user 一撳就彈個 WebApp 出嚟
        var button = InlineKeyboardButton.WithWebApp(
            "撳我搵車 🚌💨", 
            new WebAppInfo { Url = webAppUrl }
        );

        var keyboard = new InlineKeyboardMarkup(button);

        // Send a playful Cantonese message with the launch button
        // 用返 Mud9bot 嘅搞怪口吻覆 user
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "想知架車幾時到？撳下面粒掣入去睇吓啦，唔使再喺條街度戇居居等喇！🚀",
            replyMarkup: keyboard
        );
    }
}