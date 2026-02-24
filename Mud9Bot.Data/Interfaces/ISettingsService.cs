namespace Mud9Bot.Data.Interfaces;

public interface ISettingsService
{
    /// <summary>
    /// 從資料庫載入所有設定到 RAM 快取 (啟動或手動重整時呼叫)
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 檢查目前是否處於維護模式
    /// </summary>
    bool IsMaintenanceMode();

    /// <summary>
    /// 獲取維護模式時顯示的公告文字
    /// </summary>
    string GetMaintenanceMessage();

    /// <summary>
    /// 判斷是否應該對該 Chat 發送維護通知 (實作 10 分鐘防刷邏輯)
    /// </summary>
    bool ShouldNotifyMaintenance(long chatId);

    /// <summary>
    /// 獲取通用的系統設定值
    /// </summary>
    string GetSetting(string key, string defaultValue = "");

    /// <summary>
    /// 立即刷新 RAM 中的特定設定 (由 Admin API 呼叫，不需重啟)
    /// </summary>
    void RefreshSetting(string key, string value);
}