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

namespace Mud9Bot.Modules;

public class AdminModule(IServiceScopeFactory scopeFactory)
{
    // Use DevOnly=true to restrict access
    [Command("sql", Description = "Execute SQL", DevOnly = true)]
    public async Task ExecuteSql(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        // Join args back to string for SQL query
        var query = string.Join(" ", args);

        if (string.IsNullOrWhiteSpace(query))
        {
            await bot.Reply(msg, "Please provide a query. Example: `SELECT * FROM users`", ct);
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

            if (query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || 
                query.TrimStart().StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = await command.ExecuteReaderAsync(ct);
                var sb = new StringBuilder();
                int rowCount = 0;

                sb.Append("```\n");
                for(int i=0; i<reader.FieldCount; i++)
                {
                    sb.Append(reader.GetName(i).PadRight(15) + " | ");
                }
                sb.AppendLine("\n" + new string('-', 30));

                while (await reader.ReadAsync(ct))
                {
                    rowCount++;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                        if (val!.Length > 15) val = val.Substring(0, 12) + "...";
                        sb.Append(val.PadRight(15) + " | ");
                    }
                    sb.AppendLine();
                    
                    if (sb.Length > 2000)
                    {
                        sb.AppendLine("\n... (Truncated)");
                        break;
                    }
                }
                sb.Append("```");

                var header = $"*Number of records: {rowCount}*\n";
                await bot.Reply(msg, header + sb.ToString(), ct);
            }
            else
            {
                var rows = await command.ExecuteNonQueryAsync(ct);
                await bot.Reply(msg, $"*Command Executed.*\nRows affected: {rows}", ct);
            }
        }
        catch (Exception ex)
        {
            await bot.Reply(msg, $"*SQL Error:*\n```\n{ex.Message}\n```", ct);
        }
    }
    
    
    [Command("raw", DevOnly = true)]
    public async Task RawCommand(ITelegramBotClient bot, Message message, string[] args, CancellationToken ct)
    {
        // 1. Ensure it's a reply
        if (message.ReplyToMessage == null)
        {
            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: "你想睇邊條 message 嘅 Raw data？對住佢 `/raw` 啦！",
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: ct);
            return;
        }

        // 2. Serialize to JSON
        var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        string json = JsonSerializer.Serialize(message.ReplyToMessage, options);

        // 3. Wrap in HTML code block to handle JSON characters safely
        string safeJson = WebUtility.HtmlEncode(json);
        string response = $"<pre><code class=\"language-json\">{safeJson}</code></pre>";

        await bot.SendMessage(
            chatId: message.Chat.Id,
            text: response,
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: ct
        );
    }
}