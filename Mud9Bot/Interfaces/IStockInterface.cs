namespace Mud9Bot.Interfaces;

public interface IStockService
{
    /// <summary>
    /// 獲取恆生指數即時數據
    /// </summary>
    Task<string> GetHsiAsync(CancellationToken ct = default);

    /// <summary>
    /// 根據股票代號獲取港股即時數據
    /// </summary>
    Task<string> GetStockAsync(string code, CancellationToken ct = default);
}