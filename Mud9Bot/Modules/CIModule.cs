using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Extensions; 

namespace Mud9Bot.Modules;

public class CIModule(IGitHubService githubService, ILogger<CIModule> logger)
{
    [CallbackQuery("GH_BUILD", DevOnly = true)]
    public async Task HandleBuildClick(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        await ProcessCiTrigger(bot, query, "build", ct);
    }

    [CallbackQuery("GH_DEPLOY", DevOnly = true)]
    public async Task HandleDeployClick(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        await ProcessCiTrigger(bot, query, "deploy", ct);
    }

    [CallbackQuery("GH_CANCEL", DevOnly = true)]
    public async Task HandleCancelClick(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        await bot.AnswerCallbackQuery(query.Id, "❌ 操作已取消 / Operation Cancelled", cancellationToken: ct);
        
        try
        {
            // 🚀 Using the library's ToHtml() extension
            string originalHtml = query.Message!.ToHtml();
            
            await bot.EditMessageText(
                chatId: query.Message.Chat.Id,
                messageId: query.Message.MessageId,
                text: $"<s>{originalHtml}</s>\n\n❌ <b>操作已取消 / Operation Cancelled</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch { }
    }

    private async Task ProcessCiTrigger(ITelegramBotClient bot, CallbackQuery query, string actionType, CancellationToken ct)
    {
        var data = query.Data!; 
        var parts = data.Split('+');
        if (parts.Length < 3) 
        {
            await bot.AnswerCallbackQuery(query.Id, "⚠️ 按鈕格式過期 / Button Data Expired", showAlert: true, cancellationToken: ct);
            return;
        }

        string target = parts[1].ToUpper();
        string sha = parts[2];
        string eventType = $"trigger_{actionType}_{target.ToLower()}";

        string toastMsg = actionType == "build" 
            ? $"⚙️ 通知 GitHub 編譯 {target}... / Notifying GitHub to Build..." 
            : $"🚀 啟動 {target} 部署... / Starting {target} Deployment...";
        
        await bot.AnswerCallbackQuery(query.Id, toastMsg, cancellationToken: ct);

        var success = await githubService.TriggerDispatchAsync(eventType, sha, ct);

        // 🚀 Using the library's ToHtml() extension
        string originalHtml = query.Message!.ToHtml();

        if (success)
        {
            string statusZh = actionType == "build" ? "正在編譯" : "正在部署";
            string statusEn = actionType == "build" ? "Building" : "Deploying";
            
            await bot.EditMessageText(
                chatId: query.Message.Chat.Id,
                messageId: query.Message.MessageId,
                text: $"{originalHtml}\n\n⏳ <b>狀態：GitHub {statusZh} {target}... / Status: GitHub {statusEn} {target}...</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        else
        {
            await bot.EditMessageText(
                chatId: query.Message.Chat.Id,
                messageId: query.Message.MessageId,
                text: $"{originalHtml}\n\n❌ <b>錯誤：呼叫 GitHub API 失敗 / Error: GitHub API Dispatch Failed</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }
}