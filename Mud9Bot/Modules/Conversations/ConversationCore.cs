using System.Collections.Concurrent;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Mud9Bot.Modules.Conversations;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConversationAttribute : Attribute
{
    public string Trigger { get; }
    public ConversationAttribute(string trigger) => Trigger = trigger.ToLower();
    public string Description { get; set; } = "";
}

public interface IConversation
{
    string ConversationName { get; }
    bool IsEntryPoint(Update update) => false;
    Task<string?> ExecuteStepAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct);
}

public class ConversationContext
{
    public string CurrentState { get; set; } = "Start";
    public Dictionary<string, object> Data { get; set; } = new();
}

public class ConversationManager
{
    private readonly ITelegramBotClient _bot;
    private readonly Dictionary<string, IConversation> _triggerMap = new();
    private readonly Dictionary<string, IConversation> _workflowMap = new();
    private readonly ConcurrentDictionary<long, (string WorkflowName, ConversationContext Context)> _userSessions = new();

    public ConversationManager(ITelegramBotClient bot, IEnumerable<IConversation> conversations)
    {
        _bot = bot;
        foreach (var conv in conversations)
        {
            _workflowMap[conv.ConversationName] = conv;
            var attr = conv.GetType().GetCustomAttribute<ConversationAttribute>();
            if (attr != null) _triggerMap[attr.Trigger] = conv;
        }
    }

    public async Task<bool> HandleUpdateAsync(Update update, CancellationToken ct)
    {
        var user = update.Message?.From ?? update.CallbackQuery?.From;
        if (user == null) return false;

        long userId = user.Id;

        // 1. CHECK FOR START COMMANDS (PRIORITY: RESTART SESSION)
        // If user types /msettings, we restart immediately, even if they were in the middle of typing a number.
        if (update.Message?.Text is { } text && text.StartsWith("/"))
        {
            string command = ParseCommand(text);

            if (_triggerMap.TryGetValue(command, out var targetWorkflow))
            {
                // Force remove old session if exists
                _userSessions.TryRemove(userId, out _);
                
                await StartNewSessionAsync(targetWorkflow, userId, update, ct);
                return true;
            }
        }

        // 2. CHECK ACTIVE SESSION
        if (_userSessions.TryGetValue(userId, out var session))
        {
            if (_workflowMap.TryGetValue(session.WorkflowName, out var activeWorkflow))
            {
                var nextState = await activeWorkflow.ExecuteStepAsync(_bot, update, session.Context, ct);

                if (nextState == null)
                    _userSessions.TryRemove(userId, out _);
                else
                    session.Context.CurrentState = nextState;
                
                return true;
            }
        }

        // 3. FALLBACK ENTRY POINTS (Non-Command triggers)
        foreach (var workflow in _workflowMap.Values)
        {
            if (workflow.IsEntryPoint(update))
            {
                await StartNewSessionAsync(workflow, userId, update, ct);
                return true;
            }
        }

        return false;
    }

    private async Task StartNewSessionAsync(IConversation workflow, long userId, Update update, CancellationToken ct)
    {
        var context = new ConversationContext { CurrentState = "Start" };
        var nextState = await workflow.ExecuteStepAsync(_bot, update, context, ct);

        if (nextState != null)
        {
            _userSessions[userId] = (workflow.ConversationName, context);
        }
    }

    private string ParseCommand(string text)
    {
        var parts = text.Split(' ');
        var command = parts[0].Substring(1).ToLower();
        return command.Contains('@') ? command.Split('@')[0] : command;
    }
}