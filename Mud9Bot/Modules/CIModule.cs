using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules;

public class CIModule(IGitHubService githubService, ILogger<CIModule> logger)
{
    // 🚀 Handle Build Buttons (Bot & Web)
    [CallbackQuery("GH_BUILD_", DevOnly = true)]
    public async Task HandleBuildClick(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var data = query.Data!; // GH_BUILD_BOT+sha or GH_BUILD_WEB+sha
        var parts = data.Split('+');
        if (parts.Length < 2) return;

        string target = parts[0].Contains("BOT") ? "BOT" : "WEB";
        string eventType = $"trigger_build_{target.ToLower()}";
        string sha = parts[1];

        await bot.AnswerCallbackQuery(query.Id, $"⚙️ 正在通知 GitHub 編譯 {target}...", cancellationToken: ct);

        var success = await githubService.TriggerDispatchAsync(eventType, sha, ct);

        if (success)
        {
            await bot.EditMessageText(
                query.Message!.Chat.Id,
                query.Message.MessageId,
                query.Message.Text + $"\n\n⏳ <b>狀態：GitHub 正在編譯 {target}...</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    // 🚀 Handle Deploy Buttons (Bot & Web)
    [CallbackQuery("GH_DEPLOY_", DevOnly = true)]
    public async Task HandleDeployClick(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        var data = query.Data!; // GH_DEPLOY_BOT+sha or GH_DEPLOY_WEB+sha
        var parts = data.Split('+');
        if (parts.Length < 2) return;

        string target = parts[0].Contains("BOT") ? "BOT" : "WEB";
        string eventType = $"trigger_deploy_{target.ToLower()}";
        string sha = parts[1];

        await bot.AnswerCallbackQuery(query.Id, $"🚀 正在啟動 {target} 部署程序...", cancellationToken: ct);

        var success = await githubService.TriggerDispatchAsync(eventType, sha, ct);

        if (success)
        {
            await bot.EditMessageText(
                query.Message!.Chat.Id,
                query.Message.MessageId,
                query.Message.Text + $"\n\n🚀 <b>狀態：正在將 {target} 部署至生產環境...</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    // 🚀 Handle Cancel Button
    [CallbackQuery("GH_CANCEL", DevOnly = true)]
    public async Task HandleCancelClick(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
    {
        await bot.AnswerCallbackQuery(query.Id, "操作已取消。", cancellationToken: ct);
        try
        {
            await bot.EditMessageText(
                query.Message!.Chat.Id,
                query.Message.MessageId,
                $"<s>{query.Message.Text}</s>\n\n❌ <b>操作已取消</b>",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        catch { }
    }
}