using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Modules;

public class CIModule(IGitHubService githubService, ILogger<CIModule> logger)
{
    // 🚀 遵循標準：只宣告乾淨的前綴
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
        await bot.AnswerCallbackQuery(query.Id, "操作已取消。", cancellationToken: ct);
        try
        {
            await bot.EditMessageText(
                chatId: query.Message!.Chat.Id,
                messageId: query.Message.MessageId,
                text: $"<s>{query.Message.Text}</s>\n\n❌ <b>操作已取消</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch { }
    }

    private async Task ProcessCiTrigger(ITelegramBotClient bot, CallbackQuery query, string actionType, CancellationToken ct)
    {
        var data = query.Data!; 
        
        // 🚀 遵循標準：利用 '+' 完美切割參數 [0]=GH_BUILD, [1]=BOT/WEB, [2]=SHA
        var parts = data.Split('+');
        if (parts.Length < 3) return;

        string target = parts[1].ToUpper(); // "BOT" or "WEB"
        string sha = parts[2];
        
        string eventType = $"trigger_{actionType}_{target.ToLower()}";

        // 1. 立即回答以停止轉圈
        await bot.AnswerCallbackQuery(query.Id, $"⚙️ 正在通知 GitHub 執行 {actionType} {target}...", cancellationToken: ct);

        // 2. 執行 GitHub 呼叫
        var success = await githubService.TriggerDispatchAsync(eventType, sha, ct);

        if (success)
        {
            string statusText = actionType == "build" ? "正在編譯" : "正在部署";
            await bot.EditMessageText(
                query.Message!.Chat.Id,
                query.Message.MessageId,
                query.Message.Text + $"\n\n⏳ <b>狀態：GitHub {statusText} {target}...</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        else
        {
            await bot.SendMessage(query.Message!.Chat.Id, $"❌ 呼叫 GitHub API 失敗，請檢查機器人 Log。", cancellationToken: ct);
        }
    }
}