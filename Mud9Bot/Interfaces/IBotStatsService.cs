using Telegram.Bot.Types;

namespace Mud9Bot.Interfaces;

public interface IBotStatsService
{
    /// <summary>
    /// Records a raw update to count as "processed message".
    /// </summary>
    Task RecordUpdateAsync(Update update, CancellationToken ct);

    /// <summary>
    /// Records a specific engagement event (command, callback, inline query).
    /// </summary>
    Task RecordEventAsync(string eventType, string? metadata, Update update, CancellationToken ct);

    /// <summary>
    /// Forces an immediate flush of in-memory stats to the database.
    /// </summary>
    Task FlushAsync();
}