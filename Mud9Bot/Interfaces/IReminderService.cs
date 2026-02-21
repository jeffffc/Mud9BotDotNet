namespace Mud9Bot.Interfaces;

public record ReminderRequest(
    DateTime RemindTime, 
    string Content, 
    string DelayDisplay, 
    string? Recurrence = null // 新增：用於標記重複性質
);

public interface IReminderService
{
    /// <summary>
    /// 解析自然語言字串並回傳提醒請求
    /// </summary>
    ReminderRequest? ParseReminder(string text);

    /// <summary>
    /// 儲存提醒至資料庫並排程至 Quartz
    /// </summary>
    Task CreateReminderAsync(long chatId, long userId, string userName, int msgId, ReminderRequest request);

    /// <summary>
    /// 啟動時從資料庫恢復所有尚未執行的提醒
    /// </summary>
    Task RecoverPendingRemindersAsync();

    /// <summary>
    /// 刪除特定提醒並取消排程
    /// </summary>
    Task<bool> DeleteReminderAsync(int jobId, long userId);
}