using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Modules;

public class SpeechModule(
    ISpeechService speechService, 
    ILogger<SpeechModule> logger,
    IConfiguration config)
{
    [Command("speech", Description = "語音轉文字 (需回覆語音訊息使用)")]
    public async Task SpeechToTextCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. 檢查是否為回覆語音訊息
        var voice = message.ReplyToMessage?.Voice;
        if (voice == null)
        {
            await bot.Reply(message, "你想聽邊條語音呀？對住條語音用 `/speech` 啦！", ct: ct);
            return;
        }

        // 2. 第一階段回覆
        var statusMsg = await bot.Reply(message, "聽緊，比啲時間我……", ct: ct);
        var logGroupId = config.GetValue<long>("BotConfiguration:LogGroupId");

        try
        {
            // 3. 下載語音檔案
            var file = await bot.GetFile(voice.FileId, cancellationToken: ct);
            if (string.IsNullOrEmpty(file.FilePath)) throw new Exception("File path is empty");

            using var memoryStream = new MemoryStream();
            await bot.DownloadFile(file.FilePath, memoryStream, cancellationToken: ct);
            byte[] audioBytes = memoryStream.ToArray();

            // 4. 呼叫服務進行辨識
            var userId = message.From?.Id ?? 0;
            var result = await speechService.RecognizeAsync(userId, audioBytes, ct);

            // 5. 處理結果並編輯訊息
            if (result.Success)
            {
                string safeText = result.Text.EscapeHtml();
                await bot.EditMessageText(
                    chatId: message.Chat.Id,
                    messageId: statusMsg.MessageId,
                    text: $"<b>🎙 語音內容：</b>\n\n{safeText}",
                    parseMode: ParseMode.Html,
                    cancellationToken: ct
                );
            }
            else
            {
                // 如果是頻率限制，直接顯示
                if (result.ErrorCode == "LIMIT")
                {
                    await bot.EditMessageText(
                        chatId: message.Chat.Id, 
                        messageId: statusMsg.MessageId, 
                        text: result.Text, 
                        cancellationToken: ct
                    );
                }
                else
                {
                    // API 辨識失敗 (我聽唔明)
                    await bot.EditMessageText(
                        chatId: message.Chat.Id, 
                        messageId: statusMsg.MessageId, 
                        text: "我聽唔明...", 
                        cancellationToken: ct
                    );
                }
            }
        }
        catch (Exception ex)
        {
            // 6. 異常處理與日誌記錄
            // 按照你的風格：通知管理員並告知用戶
            await bot.LogException(ex, message, logGroupId, logger, ct: ct);
            
            string errorPrompt = "呢條語音搞唔掂呀，我已經話左比我主人聽，請你等佢處理下。";
            try
            {
                await bot.EditMessageText(
                    chatId: message.Chat.Id, 
                    messageId: statusMsg.MessageId, 
                    text: errorPrompt, 
                    cancellationToken: ct
                );
            }
            catch { /* Ignore if edit fails */ }
        }
    }
}