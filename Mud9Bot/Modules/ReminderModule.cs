using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Modules;

public class ReminderModule(IReminderService reminderService)
{
    [TextTrigger(@".+提我.+", Description = "Regex reminder handler")]
    public async Task HandleReminderAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.Text) || message.From == null) return;

        var request = reminderService.ParseReminder(message.Text);
        if (request == null) return;

        string fullName = (message.From.FirstName + " " + message.From.LastName).Trim();
        if (string.IsNullOrEmpty(fullName)) fullName = message.From.Username ?? "你";

        try
        {
            await reminderService.CreateReminderAsync(
                message.Chat.Id,
                message.From.Id,
                fullName,
                message.MessageId,
                request
            );

            string recurrenceMsg = request.Recurrence != null ? " (重複性任務 🔄)" : "";
            await bot.Reply(message, $"✅ 收到，我會喺 <b>{request.DelayDisplay}</b> 提你。{recurrenceMsg} #reminder", ct);
        }
        catch (InvalidOperationException ex)
        {
            // 處理超過 30 條提醒的限制
            await bot.Reply(message, $"⚠️ {ex.Message}", ct);
        }
    }
}