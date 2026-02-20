namespace Mud9Bot.Interfaces;

public record TranslationResult(string TranslatedText, string DetectedSourceLanguage);

public interface ITranslateService
{
    /// <summary>
    /// Translates text using Google Cloud Translation API with per-user rate limiting.
    /// </summary>
    Task<TranslationResult> TranslateAsync(long userId, string text, string targetLanguage = "zh-TW", CancellationToken ct = default);
}