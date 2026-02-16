using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Modules;

public class GeneralModule
{
    [Command("start", Description = "Start the bot")]
    public async Task Start(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        await bot.Reply(msg, "Hello! I am Mud9Bot using Attributes!", ct);
    }

    [Command("ping")]
    public async Task Ping(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        await bot.Reply(msg, "Pong!", ct);
    }
}