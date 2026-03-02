using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Mud9Bot.Attributes;

namespace Mud9Bot.Modules;

/// <summary>
/// Module to handle Transport (Bus & MTR) ETA requests via Telegram WebApp.
/// 處理交通 (巴士、港鐵) ETA 請求嘅 WebApp 模組。
/// </summary>
public class TransportEtaModule(IConfiguration config, ILogger<TransportEtaModule> logger)
{
    /// <summary>
    /// Handles the /transport command to launch the Transport Landing Hub.
    /// 處理 /transport 指令，用嚟開交通總覽頁面。
    /// </summary>
    [Command("transport", PrivateOnly = true)]
    public async Task HandleTransportCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await LaunchWebApp(
            bot, 
            message, 
            path: "", // Empty path points to the base URL (transport.html landing page)
            buttonText: "撳我睇交通 🧭", 
            replyText: "想搵車定搵港鐵？撳下面粒掣入去交通總覽自己揀啦！🚀", 
            ct
        );
    }
    
    /// <summary>
    /// Handles the /bus command to launch the Bus Mini App.
    /// 處理 /bus 指令，用嚟開巴士 Mini App 出嚟。
    /// </summary>
    [Command("bus", PrivateOnly = true)]
    public async Task HandleBusCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await LaunchWebApp(
            bot, 
            message, 
            path: "bus", 
            buttonText: "撳我睇巴士到站 🚌💨", 
            replyText: "想知架車幾時到？撳下面粒掣入去睇吓啦，唔使再喺條街度戇居居等喇！🚀", 
            ct
        );
    }

    /// <summary>
    /// Handles the /mtr command to launch the MTR Mini App.
    /// 處理 /mtr 指令，用嚟開港鐵 Mini App 出嚟。
    /// </summary>
    [Command("mtr", PrivateOnly = true)]
    public async Task HandleMtrCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await LaunchWebApp(
            bot, 
            message, 
            path: "mtr", 
            buttonText: "撳我睇港鐵到站 🚇💨", 
            replyText: "想知下一班港鐵幾時有？撳下面粒掣入去睇吓啦，唔使衝落去月台跑喇！🚀", 
            ct
        );
    }
    
    [Command("minibus", "gmb", PrivateOnly = true)]
    public async Task HandleMinibusCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        await LaunchWebApp(
            bot, 
            message, 
            path: "minibus", 
            buttonText: "撳我睇小巴到站 🚐💨", 
            replyText: "想知小巴幾時到？撳下面粒掣入去睇吓啦，費事排長龍等呀！🚀", 
            ct
        );
    }

    /// <summary>
    /// Shared helper method to launch the WebApp with a specific sub-path.
    /// 共用嘅發送 WebApp 方法，自動幫你處理埋 URL 同 Error Logging。
    /// </summary>
    private async Task LaunchWebApp(ITelegramBotClient bot, Message message, string path, string buttonText, string replyText, CancellationToken ct)
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
            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: "呢個功能暫時仲未用得住喎，遲啲先啦！🌚",
                cancellationToken: ct
            );

            // 2. Send an extra message to the log group to alert the admin
            // 靜靜雞話比 Log Group 聽有人撞板，等 admin 知要執嘢
            if (!string.IsNullOrEmpty(logGroupId))
            {
                var userInfo = message.From != null 
                    ? $"@{message.From.Username ?? "N/A"} ({message.From.Id})" 
                    : "Unknown User";

                await bot.SendMessage(
                    chatId: logGroupId,
                    text: $"⚠️ 報告！有人試圖用 /{path} 指令，但係 WebAppUrl 仲未 set 呀！\n\nUser: {userInfo}",
                    cancellationToken: ct
                );
            }

            logger.LogWarning("[TransportModule] WebAppUrl is missing in configuration for /{Path} command.", path);
            return;
        }

        // Safely construct the target URL (e.g., https://transport.mud9bot.info/bus)
        // 安全咁將 base URL 同 path 拼埋一齊
        var targetUrl = $"{webAppUrl.TrimEnd('/')}/{path}";

        // Create a WebApp button linking to the configured URL
        // 整返粒掣，等 user 一撳就彈個對應嘅 WebApp 出嚟
        var button = InlineKeyboardButton.WithWebApp(
            buttonText, 
            new WebAppInfo { Url = targetUrl }
        );

        var keyboard = new InlineKeyboardMarkup(button);

        // Send a playful Cantonese message with the launch button
        // 用返 Mud9bot 嘅搞怪口吻覆 user
        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: replyText,
            replyMarkup: keyboard,
            cancellationToken: ct
        );
    }
}