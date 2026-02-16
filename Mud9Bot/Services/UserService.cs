using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Services.Interfaces;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Services;

public class UserService(BotDbContext dbContext, ILogger<UserService> logger) : IUserService
{
    public async Task<BotUser> SyncUserAsync(User telegramUser, CancellationToken ct = default)
    {
        // 1. Try to find the user
        var dbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramUser.Id, ct);

        if (dbUser == null)
        {
            // 2. If not found, create a new entity
            var newUser = new BotUser
            {
                TelegramId = telegramUser.Id,
                FirstName = telegramUser.FirstName,
                LastName = telegramUser.LastName,
                Username = telegramUser.Username,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };

            try
            {
                dbContext.Users.Add(newUser);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation($"New User Added: {telegramUser.Id} ({telegramUser.Username})");
                return newUser;
            }
            catch (DbUpdateException)
            {
                // RACE CONDITION HIT: Another thread added the user while we were working.
                // Discard the new entity we just tried to add to clear the context state
                dbContext.Entry(newUser).State = EntityState.Detached;
                
                // Fetch the existing user again (it MUST exist now)
                dbUser = await dbContext.Users.FirstAsync(u => u.TelegramId == telegramUser.Id, ct);
            }
        }

        // 3. Update existing user (safe to do, last write wins is usually fine here)
        if (dbUser != null)
        {
            if (dbUser.FirstName != telegramUser.FirstName ||
                dbUser.LastName != telegramUser.LastName ||
                dbUser.Username != telegramUser.Username)
            {
                dbUser.FirstName = telegramUser.FirstName;
                dbUser.LastName = telegramUser.LastName;
                dbUser.Username = telegramUser.Username;
                dbUser.LastSeen = DateTime.UtcNow;
            }
            else
            {
                dbUser.LastSeen = DateTime.UtcNow;
            }
            
            await dbContext.SaveChangesAsync(ct);
        }

        return dbUser!;
    }

    public async Task<BotGroup?> SyncGroupAsync(Chat telegramChat, CancellationToken ct = default)
    {
        if (telegramChat.Type == ChatType.Private) return null;

        var dbGroup = await dbContext.Groups.FirstOrDefaultAsync(g => g.TelegramId == telegramChat.Id, ct);

        if (dbGroup == null)
        {
            var newGroup = new BotGroup
            {
                TelegramId = telegramChat.Id,
                Title = telegramChat.Title ?? "Unknown Group"
            };

            try
            {
                dbContext.Groups.Add(newGroup);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation($"New Group Added: {telegramChat.Id} ({telegramChat.Title})");
                return newGroup;
            }
            catch (DbUpdateException)
            {
                // Race condition handled: Group already exists
                dbContext.Entry(newGroup).State = EntityState.Detached;
                dbGroup = await dbContext.Groups.FirstAsync(g => g.TelegramId == telegramChat.Id, ct);
            }
        }

        if (dbGroup != null && dbGroup.Title != telegramChat.Title)
        {
            dbGroup.Title = telegramChat.Title ?? "Unknown Group";
            await dbContext.SaveChangesAsync(ct);
        }

        return dbGroup;
    }
    
    public async Task LogCommandUsageAsync(long userId, long chatId, string command, string args, CancellationToken ct = default)
    {
        var log = new CommandLog
        {
            UserId = userId,
            ChatId = chatId,
            Command = command,
            Args = args,
            Timestamp = DateTime.UtcNow
        };
        dbContext.CommandLogs.Add(log);
        await dbContext.SaveChangesAsync(ct);
    }
}