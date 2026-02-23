using System.Reflection;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Mud9Bot.Registries;

public class CallbackQueryRegistry
{
    private readonly Dictionary<string, (MethodInfo Method, CallbackQueryAttribute Attribute)> _handlers = new();
    private readonly ILogger<CallbackQueryRegistry> _logger;
    private readonly HashSet<long> _devIds;
    // Expose registered prefixes for statistics
    public IEnumerable<string> RegisteredPrefixes => _handlers.Keys.OrderBy(k => k);

    public CallbackQueryRegistry(ILogger<CallbackQueryRegistry> logger, IBotMetadataService metadata, IConfiguration configuration)
    {
        _logger = logger;
        // 從設定檔讀取開發者 ID 列表
        _devIds = configuration.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? new HashSet<long>();
        
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
                _handlers[attr.Prefix] = (method, attr);
                _logger.LogInformation($"Registered Callback Prefix: '{attr.Prefix}' -> {method.DeclaringType?.Name}");
            }
        }
        
        // 🚀 確保元數據與實際註冊的按鈕處理程序同步
        metadata.CallbackCount = _handlers.Values.Distinct().Count();
    }

    public async Task ExecuteAsync(ITelegramBotClient bot, CallbackQuery query, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var data = query.Data;
        if (string.IsNullOrEmpty(data)) return;

        // Find matching handler. 
        // We match if data starts with "prefix:" (standard convention) or is exactly "prefix".
        var handlerEntry = _handlers.FirstOrDefault(kvp => 
            data.StartsWith(kvp.Key + "+") || data.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

        if (handlerEntry.Value.Method == null) return;

        var (method, attr) = handlerEntry.Value;
        
        // 🚀 執行 DevOnly 權限檢查
        if (attr.DevOnly && !_devIds.Contains(query.From.Id))
        {
            _logger.LogWarning("Unauthorized DevOnly callback attempt by User {UserId} on prefix {Prefix}", query.From.Id, handlerEntry.Key);
            await bot.AnswerCallbackQuery(
                query.Id, 
                "🚫 你無權使用此開發者功能。", 
                showAlert: true, 
                cancellationToken: ct);
            return;
        }
        
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