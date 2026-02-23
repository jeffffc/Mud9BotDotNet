using Mud9Bot.Models;

namespace Mud9Bot.Interfaces;

public interface IGasService
{
    /// <summary>
    /// 從消委會 API 更新最新油價並同步至快取
    /// </summary>
    Task UpdatePricesAsync(CancellationToken ct = default);

    /// <summary>
    /// 獲取記憶體中快取的油價數據
    /// </summary>
    List<GasPriceData> GetCachedPrices();

    /// <summary>
    /// 獲取最後更新時間
    /// </summary>
    DateTime LastUpdated { get; }
}