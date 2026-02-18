using Mud9Bot.Data.Entities;

namespace Mud9Bot.Services.Interfaces;

public interface IWinePlasticService
{
    // Checks if the user has a limit row, creates if missing. Returns (WineLeft, PlasticLeft).
    Task<(int WineLeft, int PlasticLeft)> GetOrCreateQuotaAsync(int userId, int groupId, int defaultW, int defaultP);

    // Executes the transaction: Deduct quota, Add Log, Update User Stats
    // Returns a success message or error string
    Task<string> ProcessTransactionAsync(int senderId, int targetId, int groupId, bool isWine, int defaultW, int defaultP);
}