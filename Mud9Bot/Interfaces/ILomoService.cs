namespace Mud9Bot.Interfaces;

public interface ILomoService
{
    /// <summary>
    /// 初始化，從資料庫載入所有排除字詞到 RAM 快取
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 獲取所有排除字詞的集合 (HashSet 提供 O(1) 查詢效能)
    /// </summary>
    HashSet<string> GetIgnoreWords();

    /// <summary>
    /// 新增字詞並重新整理快取
    /// </summary>
    Task<bool> AddWordAsync(string word);

    /// <summary>
    /// 刪除字詞並重新整理快取
    /// </summary>
    Task<bool> RemoveWordAsync(string word);
}