using System.Reflection;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Mud9Bot.Interfaces;
using Mud9Bot.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Registries;

public class CommandRegistry
{
    private readonly Dictionary<string, (MethodInfo Method, Type Type, CommandAttribute Attribute)> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CommandRegistry> _logger;
    private readonly IErrorReporter _errorReporter;
    private readonly HashSet<long> _devIds;
    
    // Expose registered triggers for statistics
    public IEnumerable<string> RegisteredTriggers => _commands.Keys.OrderBy(k => k);

    public CommandRegistry(
        ILogger<CommandRegistry> logger, 
        IConfiguration configuration,
        IErrorReporter errorReporter,
        IBotMetadataService metadata) // Inject Metadata Service
    {
        _logger = logger;
        _errorReporter = errorReporter;
        _devIds = configuration.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? [];

        ScanForCommands();
        
        // Ensure metadata is in sync with actual registered commands
        metadata.CommandCount = _commands.Count;
    }

    private void ScanForCommands()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in types)
        {
            var methods = type.GetMethods()
                .Where(m => m.GetCustomAttribute<CommandAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<CommandAttribute>()!;
                if (attr.Inactive) continue;

                // 將所有的別名 (Aliases) 都註冊到同一個處理程序
                foreach (var trigger in attr.Triggers)
                {
                    if (_commands.ContainsKey(trigger))
                    {
                        _logger.LogWarning($"Duplicate command trigger detected: '/{trigger}' in {type.Name}. Skipping.");
                        continue;
                    }
                    _commands[trigger] = (method, type, attr);
                    _logger.LogInformation($"Registered command '/{trigger}' -> {type.Name}.{method.Name}");
                }
            }
        }
    }

    public async Task ExecuteAsync(string trigger, string[] args, ITelegramBotClient bot, Message message, IServiceProvider scopedProvider, CancellationToken ct)
    {
        if (!_commands.TryGetValue(trigger, out var cmdInfo)) return;

        var (method, type, attr) = cmdInfo;
        var userId = message.From?.Id ?? 0;
        var chatType = message.Chat.Type;

        if (attr.DevOnly && !_devIds.Contains(userId))
        {
            await bot.Reply(message, "🚫 You are not the Dev!", ct);
            return; 
        }

        if (attr.PrivateOnly && chatType != ChatType.Private)
        {
            await bot.Reply(message, "🚫 This command can only be used in private chats.", ct);
            return;
        }

        if (attr.GroupOnly && chatType != ChatType.Group && chatType != ChatType.Supergroup)
        {
            await bot.Reply(message, "🚫 This command can only be used in groups.", ct);
            return;
        }

        if (attr.AdminOnly && (message.Chat.Type is ChatType.Group or ChatType.Supergroup))
        {
            var chatMember = await bot.GetChatMember(message.Chat.Id, userId, ct);
            if (chatMember.Status is not (ChatMemberStatus.Administrator or ChatMemberStatus.Creator) && !_devIds.Contains(userId))
            {
                await bot.Reply(message, "🚫 This command is for group admins only.", ct);
                return;
            }
        }
        
        try 
        {
            var userService = scopedProvider.GetService(typeof(IUserService)) as IUserService;
            if (userService != null)
            {
                var rawArgs = string.Join(" ", args);
                if (rawArgs.Length > 500) rawArgs = rawArgs.Substring(0, 500);
                await userService.LogCommandUsageAsync(userId, message.Chat.Id, trigger, rawArgs, ct);
            }
        }
        catch (Exception logEx) { _logger.LogError(logEx, "Failed to log command usage to DB."); }

        object? instance = method.IsStatic ? null : scopedProvider.GetService(type);
        if (!method.IsStatic && instance == null)
        {
            _logger.LogError($"Could not resolve {type.Name}. Did you register it in Program.cs?");
            return;
        }

        try
        {
            var result = method.Invoke(instance, new object[] { bot, message, args, ct });
            if (result is Task task) await task;
        }
        catch (Exception ex)
        {
            await _errorReporter.ReportErrorAsync(ex.InnerException ?? ex, message, ct);
        }
    }
}