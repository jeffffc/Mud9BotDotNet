namespace Mud9Bot.Interfaces;

public interface IGitHubService
{
    /// <summary>
    /// 觸發 GitHub Actions 的 repository_dispatch 事件
    /// </summary>
    /// <param name="eventType">對應 deploy.yml 中的 types (如 trigger_build_bot)</param>
    /// <param name="sha">要處理的 Commit Hash</param>
    Task<bool> TriggerDispatchAsync(string eventType, string sha, CancellationToken ct);
}