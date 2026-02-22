using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Interfaces;

public interface IInlineQueryHandler
{
    /// <summary>
    /// Processes incoming inline queries from users.
    /// </summary>
    Task HandleAsync(ITelegramBotClient bot, InlineQuery query, CancellationToken ct);
}