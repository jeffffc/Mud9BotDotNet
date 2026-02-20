using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.RegularExpressions;
using System.Net;
using System.Collections.Concurrent;

namespace Mud9Bot.Modules;

public class TranslateModule(ITranslateService translateService)
{
    // 記憶體快取：使用 ConcurrentDictionary 確保線程安全
    // Key 格式為 "{targetLanguage}:{originalText}"
    private static readonly ConcurrentDictionary<string, TranslationResult> _resultCache = new();

    [Command("t", "translate", Description = "翻譯內容 (可回覆訊息使用)")]
    public async Task TranslateCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        string textToTranslate = "";

        // 1. 優先權：回覆其他訊息
        if (message.ReplyToMessage != null && !string.IsNullOrWhiteSpace(message.ReplyToMessage.Text))
        {
            textToTranslate = message.ReplyToMessage.Text;
        }
        // 2. 備案：指令後方跟隨的文字
        else if (args.Length > 0)
        {
            textToTranslate = string.Join(" ", args);
        }
        else
        {
            await bot.Reply(message, "你要翻譯咩內容呀？對住條 message 用 `/t` 或者直接打 `/t [內容]` 啦！", ct);
            return;
        }

        var userId = message.From?.Id ?? 0;
        await bot.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: ct);

        try
        {
            // 智能邏輯：檢測中文字符比例
            var nonWhitespaceText = Regex.Replace(textToTranslate, @"\s+", "");
            int totalChars = nonWhitespaceText.Length;
            int chineseChars = Regex.Matches(nonWhitespaceText, @"\p{IsCJKUnifiedIdeographs}").Count;

            double ratio = totalChars > 0 ? (double)chineseChars / totalChars : 0;
            string targetLanguage = ratio > 0.5 ? "en" : "zh-TW";

            // --- 快取檢查邏輯 ---
            string cacheKey = $"{targetLanguage}:{textToTranslate.Trim()}";
            TranslationResult result;

            if (_resultCache.TryGetValue(cacheKey, out var cachedResult))
            {
                result = cachedResult;
            }
            else
            {
                // 執行翻譯 - 傳入 userId 以便 Service 進行頻率限制追蹤
                result = await translateService.TranslateAsync(userId, textToTranslate, targetLanguage, ct);

                // 如果回傳結果標記為頻率限制或錯誤，直接回覆並跳過快取
                if (result.DetectedSourceLanguage == "limit" || result.DetectedSourceLanguage == "err")
                {
                    await bot.Reply(message, result.TranslatedText, ct);
                    return;
                }

                // 只有成功的翻譯結果才存入快取 (考慮到內存，這裡可以視情況限制快取大小)
                _resultCache.TryAdd(cacheKey, result);
            }

            // 使用 EscapeHtml() 處理翻譯結果，確保 Telegram HTML 解析正確
            string safeResult = result.TranslatedText.EscapeHtml();
            
            // 產生可讀的語言標籤
            string sourceName = MapLanguageName(result.DetectedSourceLanguage);
            string targetName = MapLanguageName(targetLanguage);
            
            string response = $"🌍 <b>{sourceName} -> {targetName}</b>\n\n{safeResult}";

            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: response,
                parseMode: ParseMode.Html,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            await bot.Reply(message, $"❌ 翻譯出咗少少問題：{ex.Message}", ct);
        }
    }

    /// <summary>
    /// 將 ISO 語言代碼映射為易讀的名稱
    /// </summary>
    private string MapLanguageName(string code)
    {
        return code.ToLower() switch
        {
            "zh-tw" or "zh-hk" or "zh" => "繁體中文",
            "zh-cn" => "簡體中文",
            "en" => "English",
            "ja" => "日本語",
            "ko" => "한국어",
            "fr" => "Français",
            "de" => "Deutsch",
            "es" => "Español",
            "ru" => "Русский",
            "vi" => "Tiếng Việt",
            "th" => "ไทย",
            _ => code.ToUpper()
        };
    }
}