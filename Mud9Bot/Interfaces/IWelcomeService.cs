using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Interfaces;

public interface IWelcomeService
{
    /// <summary>
    /// Processes the NewChatMembers event, formatting and sending the customized welcome message.
    /// </summary>
    Task HandleNewChatMembersAsync(ITelegramBotClient bot, Message message, CancellationToken ct);

    /// <summary>
    /// Processes the LeftChatMember event, sending a notification if a bot is removed.
    /// </summary>
    Task HandleLeftChatMemberAsync(ITelegramBotClient bot, Message message, CancellationToken ct);
}