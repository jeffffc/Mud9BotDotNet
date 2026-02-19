using Microsoft.EntityFrameworkCore;
using Mud9Bot.Attributes;
using Mud9Bot.Data;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Data;
using System.Text;

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
}