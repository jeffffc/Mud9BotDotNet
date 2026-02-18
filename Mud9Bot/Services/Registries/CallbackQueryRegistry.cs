using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Mud9Bot.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Services.Registries;

public class CallbackQueryRegistry
{
    private readonly Dictionary<string, MethodInfo> _handlers = new();

    public CallbackQueryRegistry()
    {
        // Scan assembly for methods with [CallbackQuery]
        var methods = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttribute<CallbackQueryAttribute>() != null);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<CallbackQueryAttribute>();
            if (attr != null)
            {
                _handlers[attr.Prefix] = method;
            }
        }
    }

    public async Task ExecuteAsync(ITelegramBotClient bot, CallbackQuery query, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var data = query.Data;
        if (string.IsNullOrEmpty(data)) return;

        // Find matching handler. 
        // We match if data starts with "prefix:" (standard convention) or is exactly "prefix".
        var handlerEntry = _handlers.FirstOrDefault(kvp => 
            data.StartsWith(kvp.Key + ":") || data.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

        if (handlerEntry.Value == null) return;

        var method = handlerEntry.Value;
        var moduleType = method.DeclaringType;
        if (moduleType == null) return;

        try
        {
            // Resolve the Module (e.g. FortuneModule) from DI, injecting its specific dependencies (IFortuneService, etc.)
            var module = ActivatorUtilities.CreateInstance(serviceProvider, moduleType);

            // Invoke the method. Expected signature: Task Method(ITelegramBotClient, CallbackQuery, CancellationToken)
            var result = method.Invoke(module, new object[] { bot, query, ct });
            if (result is Task task)
            {
                await task;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing callback handler for {handlerEntry.Key}: {ex}");
            await bot.AnswerCallbackQuery(query.Id, "System Error handling callback.", cancellationToken: ct);
        }
    }
}