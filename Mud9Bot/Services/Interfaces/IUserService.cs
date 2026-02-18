using Mud9Bot.Data;
using Telegram.Bot.Types;
using Mud9Bot.Data.Entities;

namespace Mud9Bot.Services.Interfaces;

public interface IUserService
{
    // Syncs a Telegram User to the Database (Create or Update)
    Task<BotUser> SyncUserAsync(User telegramUser, CancellationToken ct = default);

    // Syncs a Telegram Chat (Group/Channel) to the Database
    Task<BotGroup?> SyncGroupAsync(Chat telegramChat, CancellationToken ct = default);
    
    // Logs a command execution
    Task LogCommandUsageAsync(long userId, long chatId, string command, string args, CancellationToken ct = default);
}