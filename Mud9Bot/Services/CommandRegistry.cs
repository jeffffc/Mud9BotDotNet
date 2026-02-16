using System.Reflection;
using Mud9Bot.Attributes;
using Mud9Bot.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Mud9Bot.Services.Interfaces;
using Mud9Bot.Services.Logging;

namespace Mud9Bot.Services;

public class CommandRegistry
{
    private readonly Dictionary<string, (MethodInfo Method, Type Type, CommandAttribute Attribute)> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CommandRegistry> _logger;
    private readonly IErrorReporter _errorReporter; // Injected dependency
    private readonly HashSet<long> _devIds;

    public CommandRegistry(
        ILogger<CommandRegistry> logger, 
        IConfiguration configuration,
        IErrorReporter errorReporter) // Inject IErrorReporter
    {
        _logger = logger;
        _errorReporter = errorReporter;
        
        // Load Dev IDs from config
        _devIds = configuration.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? [];

        ScanForCommands();
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
                
                // Skip if Inactive
                if (attr.Inactive) continue;

                _commands[attr.Trigger] = (method, type, attr);
                _logger.LogInformation($"Registered command '/{attr.Trigger}' -> {type.Name}.{method.Name}");
            }
        }
    }

    public async Task ExecuteAsync(string trigger, string[] args, ITelegramBotClient bot, Message message, IServiceProvider scopedProvider, CancellationToken ct)
    {
        if (!_commands.TryGetValue(trigger, out var cmdInfo))
            return;

        var (method, type, attr) = cmdInfo;
        var userId = message.From?.Id ?? 0;
        var chatType = message.Chat.Type;

        // --- Checks ---
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

        if (attr.AdminOnly)
        {
            if (message.Chat.Type is ChatType.Group or ChatType.Supergroup)
            {
                var chatMember = await bot.GetChatMember(message.Chat.Id, userId, ct);
                if (chatMember.Status is not (ChatMemberStatus.Administrator or ChatMemberStatus.Creator) && !_devIds.Contains(userId))
                {
                    await bot.Reply(message, "🚫 This command is for group admins only.", ct);
                    return;
                }
            }
        }
        // -------------
        
        // --- Log Command Usage ---
        // Resolve IUserService from the scope
        try 
        {
            var userService = scopedProvider.GetService(typeof(IUserService)) as IUserService;
            if (userService != null)
            {
                var rawArgs = string.Join(" ", args);
                // Truncate args if too long to prevent DB errors
                if (rawArgs.Length > 500) rawArgs = rawArgs.Substring(0, 500);
                
                await userService.LogCommandUsageAsync(userId, message.Chat.Id, trigger, rawArgs, ct);
            }
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to log command usage to DB.");
            // Do not stop execution if logging fails
        }
        // -------------------------

        // Resolve instance from SCOPED provider
        object? instance = null;
        if (!method.IsStatic)
        {
            // This is the key change: resolving from the scope passed from UpdateHandler
            instance = scopedProvider.GetService(type);
            if (instance == null)
            {
                _logger.LogError($"Could not resolve {type.Name}. Did you register it in Program.cs?");
                return;
            }
        }

        try
        {
            var parameters = new object[] { bot, message, args, ct };
            var result = method.Invoke(instance, parameters);

            if (result is Task task)
            {
                await task;
            }
        }
        catch (Exception ex)
        {
            var actualException = ex.InnerException ?? ex;
            await _errorReporter.ReportErrorAsync(actualException, message, ct);
        }
    }
}