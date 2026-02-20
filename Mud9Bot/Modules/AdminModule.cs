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

namespace Mud9Bot.Modules;

public class AdminModule(IServiceScopeFactory scopeFactory)
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
}