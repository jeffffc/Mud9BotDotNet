namespace Mud9Bot.Interfaces;

public record SpeechResult(bool Success, string Text, string ErrorCode = "");

public interface ISpeechService
{
    /// <summary>
    /// 將語音位元組轉換為文字。支援每用戶頻率限制。
    /// </summary>
    Task<SpeechResult> RecognizeAsync(long userId, byte[] audioData, CancellationToken ct = default);
}