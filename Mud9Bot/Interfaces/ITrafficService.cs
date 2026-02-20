using Mud9Bot.Services;

namespace Mud9Bot.Interfaces;

public interface ITrafficService
{
    // --- RTHK 交通消息 ---
    Task<string> GetTrafficNewsAsync(CancellationToken ct = default);

    // --- 交通快拍 (Snapshot) ---
    /// <summary>
    /// 在機器人啟動時呼叫，從政府 XML API 載入所有攝影機位置
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 獲取記憶體中快取的區域與攝影機資料
    /// </summary>
    List<TrafficRegion> GetRegions();
}