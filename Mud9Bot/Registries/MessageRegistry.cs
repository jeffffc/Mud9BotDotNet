using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Mud9Bot.Attributes;
using Mud9Bot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Registries;

public class MessageRegistry
{
    private readonly List<(Regex Regex, MethodInfo Method, Type Type, TextTriggerAttribute Attribute)> _handlers = new();
    private readonly ILogger<MessageRegistry> _logger;
    private readonly IErrorReporter _errorReporter;
    private readonly IBotStatsService _botStatsService; // 🚀 注入統計服務
    private readonly HashSet<long> _devIds;

    public IEnumerable<string> RegisteredPatterns => _handlers.Select(h => h.Attribute.Pattern).OrderBy(p => p);

    public MessageRegistry(
        ILogger<MessageRegistry> logger,
        IConfiguration configuration,
        IErrorReporter errorReporter,
        IBotMetadataService metadata,
        IBotStatsService botStatsService)
    {
        _logger = logger;
        _errorReporter = errorReporter;
        _botStatsService = botStatsService;
        _devIds = configuration.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? [];

        ScanForMessageTriggers();
        
        metadata.MessageTriggerCount = _handlers.Count;
    }

    private void ScanForMessageTriggers()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in types)
        {
            var methods = type.GetMethods()
                .Where(m => m.GetCustomAttribute<TextTriggerAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<TextTriggerAttribute>()!;
                if (attr.Inactive) continue;

                try 
                {
                    var regex = new Regex(attr.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    _handlers.Add((regex, method, type, attr));
                    _logger.LogInformation($"Registered Text Trigger '{attr.Pattern}' -> {type.Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to compile regex pattern '{attr.Pattern}' in {type.Name}.{method.Name}.");
                }
            }
        }
    }

    public async Task ExecuteAsync(ITelegramBotClient bot, Message message, IServiceProvider scopedProvider, CancellationToken ct)
    {
        var text = message.Text;
        if (string.IsNullOrEmpty(text)) return;

        var userId = message.From?.Id ?? 0;
        var chatType = message.Chat.Type;

        foreach (var handler in _handlers)
        {
            var match = handler.Regex.Match(text);
            if (!match.Success) continue;

            var (regex, method, type, attr) = handler;

            if (attr.DevOnly && !_devIds.Contains(userId)) continue;
            if (attr.PrivateOnly && chatType != ChatType.Private) continue;
            if (attr.GroupOnly && chatType != ChatType.Group && chatType != ChatType.Supergroup) continue;
            
            if (attr.AdminOnly && (chatType is ChatType.Group or ChatType.Supergroup))
            {
                try 
                {
                    var chatMember = await bot.GetChatMember(message.Chat.Id, userId, ct);
                    if (chatMember.Status is not (ChatMemberStatus.Administrator or ChatMemberStatus.Creator) && !_devIds.Contains(userId))
                        continue;
                } 
                catch { continue; }
            }
            
            // 🚀 關鍵修正：紀錄 Regex 觸發事件 (分類為 interaction，名稱帶有 regex 前綴)
            // 將 Update 包裝回 Update 對象傳遞給統計服務
            var dummyUpdate = new Update { Message = message };
            await _botStatsService.RecordEventAsync("interaction", $"regex_{attr.Description ?? method.Name}", dummyUpdate, ct);


            object? instance = method.IsStatic ? null : scopedProvider.GetService(type);
            if (!method.IsStatic && instance == null) continue;

            try
            {
                var parameters = method.GetParameters();
                object[] invokeArgs;

                // 🚀 ENHANCED: Support for Command-style signatures (string[] args)
                if (parameters.Length == 4)
                {
                    if (parameters[2].ParameterType == typeof(Match))
                    {
                        invokeArgs = new object[] { bot, message, match, ct };
                    }
                    else if (parameters[2].ParameterType == typeof(string[]))
                    {
                        // If it's a Command signature, pass an empty array as args
                        invokeArgs = new object[] { bot, message, Array.Empty<string>(), ct };
                    }
                    else
                    {
                        _logger.LogWarning($"Method {method.Name} has unknown 4-param signature.");
                        continue;
                    }
                }
                else if (parameters.Length == 3)
                {
                    invokeArgs = new object[] { bot, message, ct };
                }
                else
                {
                    continue;
                }

                var result = method.Invoke(instance, invokeArgs);
                if (result is Task task) await task;
            }
            catch (Exception ex)
            {
                await _errorReporter.ReportErrorAsync(ex.InnerException ?? ex, message, ct);
            }
        }
    }
}