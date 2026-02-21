using Mud9Bot.Models;

namespace Mud9Bot.Interfaces;

public interface INewsService
{
    /// <summary>
    /// 更新所有分類的新聞快取
    /// </summary>
    Task UpdateAllNewsAsync(CancellationToken ct = default);

    /// <summary>
    /// 獲取指定分類的新聞清單 (最多 5 則)
    /// </summary>
    List<NewsArticle> GetNews(NewsCategory category);
}