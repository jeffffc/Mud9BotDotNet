using Mud9Bot.Data.Entities;

namespace Mud9Bot.Interfaces;

public record GroupStatEntry(string Name, int Total);

public interface IWinePlasticService
{
    // Checks if the user has a limit row, creates if missing. Returns (WineLeft, PlasticLeft).
    Task<(int WineLeft, int PlasticLeft)> GetOrCreateQuotaAsync(int userId, int groupId);

    // Executes the transaction: Deduct quota, Add Log, Update User Stats
    // Returns a success message or error string
    Task<(bool, string)> ProcessTransactionAsync(int senderId, int targetId, int groupId, bool isWine, int num);

    Task<(bool, string)> ProcessTransactionByTelegramIdAsync(long senderTgId, long targetTgId, long groupTgId, bool isWine, int num);

    Task<(int WineCount, int PlasticCount, int WineLimit, int PlasticLimit)> GetPersonalStatsAsync(long telegramUserId,
        long telegramGroupId);
    
    /// <summary>
    /// 獲取群組內的酒膠排行統計 (Top 5)
    /// </summary>
    Task<(List<GroupStatEntry> WineTop, List<GroupStatEntry> PlasticTop)> GetGroupStatsAsync(long telegramGroupId);
    
    // New method for manual reset or job execution
    Task<int> ResetDailyQuotasAsync();
}