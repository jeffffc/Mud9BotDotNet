using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;

namespace Mud9Bot.Modules;

public class StockModule(IStockService stockService, ILogger<StockModule> logger)
{
    [Command("stocks", "stock", Description = "查詢恆指或特定港股股價")]
    public async Task StocksCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. 發送等待訊息 (對應 Python 舊碼的特務等待邏輯)
        var sentMessage = await bot.SendMessage(
            chatId: message.Chat.Id,
            text: "我派咗最頂尖嘅特務幫你睇，你等陣……🕵️‍♂️",
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );

        string resultMsg;

        if (args.Length == 0)
        {
            // 查詢恆指
            resultMsg = await stockService.GetHsiAsync(ct);
        }
        else if (args.Length == 1)
        {
            string input = args[0];
            // 檢查是否為純數字代號
            if (input.All(char.IsDigit))
            {
                resultMsg = await stockService.GetStockAsync(input, ct);
            }
            else
            {
                resultMsg = "💡 用 <code>/stocks</code> 睇恆指，或者 <code>/stocks 1234</code> 查相應股票代號。";
            }
        }
        else
        {
            resultMsg = "💡 用 <code>/stocks</code> 睇恆指，或者 <code>/stocks 1234</code> 查相應股票代號。";
        }

        // 2. 編輯原本的等待訊息以顯示結果 (HTML 模式)
        try
        {
            await bot.EditMessageText(
                chatId: message.Chat.Id,
                messageId: sentMessage.MessageId,
                text: resultMsg,
                parseMode: ParseMode.Html,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update stock result message.");
        }
    }
}