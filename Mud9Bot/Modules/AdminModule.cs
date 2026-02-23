using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Data;
using System.Text;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.DependencyInjection;
using Mud9Bot.Interfaces;
using Mud9Bot.Registries;

namespace Mud9Bot.Modules;

public class AdminModule(
    IServiceScopeFactory scopeFactory, 
    CommandRegistry commandRegistry, 
    CallbackQueryRegistry callbackRegistry,
    MessageRegistry messageRegistry,
    IBotMetadataService metadata)
{
    [Command("msql", Description = "Execute raw SQL query", DevOnly = true)]
    public async Task ExecuteSql(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        var query = string.Join(" ", args);

        if (string.IsNullOrWhiteSpace(query))
        {
            await bot.Reply(msg, "請提供 SQL 語句。例如：<code>SELECT * FROM users</code>", ct);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) 
                await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = query;

            string upperQuery = query.TrimStart().ToUpper();
            if (upperQuery.StartsWith("SELECT") || upperQuery.StartsWith("WITH"))
            {
                using var reader = await command.ExecuteReaderAsync(ct);
                var sb = new StringBuilder();
                int rowCount = 0;

                sb.AppendLine("<pre>");
                
                // Header row
                for(int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i).EscapeHtml();
                    sb.Append(colName.PadRight(15) + " | ");
                }
                sb.AppendLine("\n" + new string('-', reader.FieldCount * 18));

                // Data rows
                while (await reader.ReadAsync(ct))
                {
                    rowCount++;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var rawVal = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "NULL";
                        if (rawVal.Length > 15) rawVal = rawVal.Substring(0, 12) + "...";
                        
                        sb.Append(rawVal.EscapeHtml().PadRight(15) + " | ");
                    }
                    sb.AppendLine();
                    
                    if (sb.Length > 3000)
                    {
                        sb.AppendLine("\n... (結果過長已截斷)");
                        break;
                    }
                }
                sb.Append("</pre>");

                var header = $"<b>📊 SQL 執行結果 (共 {rowCount} 筆紀錄)</b>\n";
                await bot.Reply(msg, header + sb.ToString(), ct);
            }
            else
            {
                var rows = await command.ExecuteNonQueryAsync(ct);
                await bot.Reply(msg, $"✅ <b>指令執行成功</b>\n受影響行數：<code>{rows}</code>", ct);
            }
        }
        catch (Exception ex)
        {
            string safeError = ex.Message.EscapeHtml();
            await bot.Reply(msg, $"❌ <b>SQL 錯誤</b>\n<pre>{safeError}</pre>", ct);
        }
    }
    
    [Command("raw", DevOnly = true, Description = "View raw JSON of a message")]
    public async Task RawCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        if (message.ReplyToMessage == null)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: "你想睇邊條 message 嘅 Raw data？對住佢用 <code>/raw</code> 啦！",
                parseMode: ParseMode.Html,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: ct);
            return;
        }

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true, 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles 
        };
        
        string json = JsonSerializer.Serialize(message.ReplyToMessage, options);

        if (json.Length > 4000)
        {
            json = json.Substring(0, 3900) + "\n\n... (Data truncated)";
        }

        string safeJson = json.EscapeHtml();
        string response = $"<b>📄 Raw Message Data:</b>\n<pre><code class=\"language-json\">{safeJson}</code></pre>";

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: response,
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
    
    [Command("botstats", Description = "Show bot registration statistics", DevOnly = true)]
    public async Task BotStatsCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        
        var commandsList = string.Join(", ", commandRegistry.RegisteredTriggers.Select(t => $"<code>/{t}</code>"));
        var callbacksList = string.Join(", ", callbackRegistry.RegisteredPrefixes.Select(p => $"<code>{p}</code>"));
        var messageTriggersList = string.Join(", ", messageRegistry.RegisteredPatterns.Select(p => $"<code>{p.EscapeHtml()}</code>"));

        var sb = new StringBuilder();
        sb.AppendLine("<b>📊 Bot Registration Stats</b>");
        sb.AppendLine();
        sb.AppendLine($"├ Version: <code>{version}</code>");
        sb.AppendLine($"├ Commands: <b>{metadata.CommandCount}</b> (Triggers: {commandRegistry.RegisteredTriggers.Count()})");
        sb.AppendLine($"├ Callbacks: <b>{metadata.CallbackCount}</b>");
        sb.AppendLine($"├ Msg Triggers: <b>{metadata.MessageTriggerCount}</b>");
        sb.AppendLine($"├ Jobs: <b>{metadata.JobCount}</b>");
        sb.AppendLine($"├ Services: <b>{metadata.ServiceCount}</b>");
        sb.AppendLine($"└ Conversations: <b>{metadata.ConversationCount}</b>");
        sb.AppendLine();
        sb.AppendLine("<b>📜 Registered Commands:</b>");
        sb.AppendLine(commandsList);
        sb.AppendLine();
        sb.AppendLine("<b>🔘 Registered Callbacks:</b>");
        sb.AppendLine(callbacksList);
        sb.AppendLine();
        sb.AppendLine("<b>💬 Registered Text Triggers:</b>");
        sb.AppendLine(string.IsNullOrEmpty(messageTriggersList) ? "None" : messageTriggersList);

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
    
    [Command("restart", DevOnly = true, Description = "安全重啟機器人")]
    public async Task RestartCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. 必須 Await 這個回覆，確保 Telegram 伺服器成功接收到訊息
        await bot.Reply(message, "🔄 收到！正在安全重啟 Mud9Bot... (Offset 已更新)", ct);

        // 2. 使用背景 Task 延遲執行退出邏輯
        // 這樣 RestartCommand 會立即回傳 Task.Completed，讓 UpdateHandler 完成該次更新循環
        _ = Task.Run(async () =>
        {
            // 給予足夠時間讓機器人核心完成更新 Offset 的動作
            await Task.Delay(1000);
            
            // 退出程序，讓 systemd 偵測到並重啟
            Environment.Exit(1);
        });
    }
}