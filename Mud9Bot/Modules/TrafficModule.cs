using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Modules;

public class TrafficModule(ITrafficService trafficService)
{
    [Command("traffic", Description = "Get latest traffic news")]
    public async Task GetTraffic(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        // If args are empty, show news. If args exist (e.g. /traffic snapshot), handle separately later.
        if (args.Length == 0)
        {
            await bot.Reply(msg, "🔄 Fetching traffic news...", ct);
            var news = await trafficService.GetTrafficNewsAsync(ct);
            await bot.Reply(msg, $"🚦 *Traffic News*\n\n{news.EscapeMarkdown()}", ct);
        }
        else
        {
            // Placeholder for Snapshot feature
            await bot.Reply(msg, "Snapshot feature coming soon!", ct);
        }
    }
}