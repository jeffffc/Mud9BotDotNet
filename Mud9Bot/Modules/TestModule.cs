using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Mud9Bot.Logging;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Modules;

public class TestModule(IHttpService httpService)
{
    [Command("test", Description = "Test HTTP Service", DevOnly = true)]
    public async Task TestHttp(ITelegramBotClient bot, Message msg, string[] args, CancellationToken ct)
    {
        var url = "https://httpbin.org/get";
        await bot.Reply(msg, $"🔄 Testing HTTP GET to `{url}`...", ct);

        try
        {
            var result = await httpService.GetStringAsync(url, ct);

            if (!string.IsNullOrEmpty(result))
            {
                // Truncate result if too long for Telegram
                if (result.Length > 3000) result = result.Substring(0, 3000) + "...";
                
                // Result is put inside a code block, so we don't need to escape it generally,
                // but we should ensure it doesn't contain closing backticks "```".
                // Ideally we would escape it, but for JSON inside code blocks it's usually okay as is.
                // However, "Success!" has '!' which is reserved.
                await bot.Reply(msg, $"✅ *Success!*\nResponse:\n```json\n{result}\n```", ct);
            }
            else
            {
                // '!' and '.' and '-' must be escaped
                await bot.Reply(msg, "❌ HTTP Request failed (returned null/empty). Check logs.", ct);
            }
        }
        catch (Exception ex)
        {
            // Escape exception message as it's untrusted input
            await bot.Reply(msg, $"❌ Exception: {ex.Message}", ct);
        }
    }
}