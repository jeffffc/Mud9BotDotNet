using Mud9Bot.Models;

namespace Mud9Bot.Interfaces;

public interface IMarkSixService
{
    /// <summary>
    /// 從網站抓取最新開獎結果並更新記憶體快取
    /// </summary>
    Task UpdateCacheAsync();

    /// <summary>
    /// 獲取記憶體中快取的最新開獎結果
    /// </summary>
    MarkSixResult? GetLatestResult();
}