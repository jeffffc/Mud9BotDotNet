using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Services;

public class WelcomeService(IGroupService groupService, ILogger<WelcomeService> logger, IConfiguration config) : IWelcomeService
{
    public async Task HandleNewChatMembersAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.NewChatMembers == null || !message.NewChatMembers.Any()) return;
        
        var inviter = message.From; // The person who added the bot/user
        var group = await groupService.GetGroupSettingsAsync(message.Chat.Id, ct);

        foreach (var user in message.NewChatMembers)
        {
            // 1. Bot Detection & Default Group Alert
            if (user.IsBot)
            {
                if (inviter != null)
                {
                    string botName = (user.FirstName + " " + user.LastName).Trim().EscapeHtml();
                    string inviterName = (inviter.FirstName + " " + inviter.LastName).Trim().EscapeHtml();

                    // Default Cantonese message sent to the group itself
                    string botJoinMsg = $"⚠️ <b>留意返：</b>\n有隻新 Bot <code>{botName}</code> (<code>{user.Id}</code>) 畀 <code>{inviterName}</code> (<code>{inviter.Id}</code>) 加咗入谷！";
                    
                    try
                    {
                        await bot.SendMessage(
                            chatId: message.Chat.Id,
                            text: botJoinMsg,
                            parseMode: ParseMode.Html,
                            cancellationToken: ct
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to send bot join message to chat {ChatId}", message.Chat.Id);
                    }
                }
                
                // Skip the normal welcome message for bots
                continue; 
            }

            // 2. Normal User Welcome Logic
            if (group == null || string.IsNullOrWhiteSpace(group.WelcomeText)) continue;

            // HTML escape variables to prevent user-supplied names from breaking ParseMode.Html
            string name = (user.FirstName + " " + user.LastName).Trim().EscapeHtml();
            string username = user.Username != null ? "@" + user.Username.EscapeHtml() : name;
            string id = user.Id.ToString();
            string lang = (user.LanguageCode ?? "Unknown").EscapeHtml();
            string title = (message.Chat.Title ?? "Group").EscapeHtml();

            // Replace placeholders (Case-insensitive)
            string text = group.WelcomeText
                .Replace("$name", name, StringComparison.OrdinalIgnoreCase)
                .Replace("$username", username, StringComparison.OrdinalIgnoreCase)
                .Replace("$id", id, StringComparison.OrdinalIgnoreCase)
                .Replace("$language", lang, StringComparison.OrdinalIgnoreCase)
                .Replace("$title", title, StringComparison.OrdinalIgnoreCase);

            try
            {
                if (!string.IsNullOrEmpty(group.WelcomePhoto))
                {
                    await bot.SendPhoto(
                        chatId: message.Chat.Id,
                        photo: InputFile.FromFileId(group.WelcomePhoto),
                        caption: text,
                        parseMode: ParseMode.Html,
                        cancellationToken: ct
                    );
                }
                else if (!string.IsNullOrEmpty(group.WelcomeGif))
                {
                    await bot.SendAnimation(
                        chatId: message.Chat.Id,
                        animation: InputFile.FromFileId(group.WelcomeGif),
                        caption: text,
                        parseMode: ParseMode.Html,
                        cancellationToken: ct
                    );
                }
                else
                {
                    await bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: text,
                        parseMode: ParseMode.Html,
                        cancellationToken: ct
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send welcome message to chat {ChatId}", message.Chat.Id);
            }
        }
    }

    public async Task HandleLeftChatMemberAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        if (message.LeftChatMember is { } leftMember)
        {
            if (leftMember.IsBot)
            {
                var remover = message.From;
                string botName = (leftMember.FirstName + " " + leftMember.LastName).Trim().EscapeHtml();
                string msgText;

                // 判斷是自己退群還是被踢走
                if (remover != null && remover.Id != leftMember.Id)
                {
                    string removerName = (remover.FirstName + " " + remover.LastName).Trim().EscapeHtml();
                    msgText = $"⚠️ <b>留意返：</b>\nBot <code>{botName}</code> (<code>{leftMember.Id}</code>) 畀 <code>{removerName}</code> (<code>{remover.Id}</code>) 踢走咗！";
                }
                else
                {
                    msgText = $"⚠️ <b>留意返：</b>\nBot <code>{botName}</code> (<code>{leftMember.Id}</code>) 自己退咗谷！";
                }

                try
                {
                    await bot.SendMessage(
                        chatId: message.Chat.Id,
                        text: msgText,
                        parseMode: ParseMode.Html,
                        cancellationToken: ct
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send bot leave message to chat {ChatId}", message.Chat.Id);
                }
            }
        }
    }
}