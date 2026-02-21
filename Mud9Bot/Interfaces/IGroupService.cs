using Mud9Bot.Data.Entities;

namespace Mud9Bot.Interfaces;

public interface IGroupService
{
    /// <summary>
    /// 獲取群組設定快取。如果快取不存在，則從 DB 載入。
    /// </summary>
    Task<BotGroup?> GetGroupSettingsAsync(long telegramId, CancellationToken ct = default);

    /// <summary>
    /// 手動更新或移除特定群組的快取（通常用於設定變更後）
    /// </summary>
    void RefreshCache(BotGroup group);
}