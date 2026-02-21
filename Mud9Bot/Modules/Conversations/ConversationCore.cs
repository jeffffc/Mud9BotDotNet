using System.Collections.Concurrent;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Mud9Bot.Data;
using Mud9Bot.Extensions;

namespace Mud9Bot.Modules.Conversations;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConversationAttribute(string trigger) : Attribute 
{
    public string Trigger { get; } = trigger;
    public string Description { get; set; } = "";
    public bool DevOnly { get; set; } = false; // Added DevOnly property
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
    public int MenuMessageId { get; set; } 
    public long ChatId { get; set; } // 新增：綁定該對話發生所在的 ChatId
    public Dictionary<string, object> Data { get; set; } = new();
}

public class ConversationManager
{
    private readonly ITelegramBotClient _bot;
    private readonly Dictionary<string, IConversation> _triggerMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IConversation> _workflowMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, (string WorkflowName, ConversationContext Context)> _userSessions = new();
    private readonly HashSet<long> _devIds; 
    
    public ConversationManager(ITelegramBotClient bot, IEnumerable<IConversation> conversations, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _bot = bot;
        _devIds = config.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? new HashSet<long>();
        
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
        long currentChatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;

        // 0. 防誤觸檢查：如果有人亂撳其他人對話中嘅按鈕，直接彈出警告！
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Message != null)
        {
            int cbMsgId = update.CallbackQuery.Message.MessageId;
            
            foreach (var kvp in _userSessions)
            {
                if (kvp.Key != userId && kvp.Value.Context.MenuMessageId == cbMsgId)
                {
                    var errmsg = Constants.NoOriginalSenderMessageList.GetAny();
                    await _bot.AnswerCallbackQuery(update.CallbackQuery.Id, errmsg, showAlert: true, cancellationToken: ct);
                    return true; 
                }
            }
        }

        // 1. CHECK FOR COMMANDS
        if (update.Message?.Text is { } text && text.StartsWith("/"))
        {
            var parts = text.Split(' ', 2);
            string command = parts[0].Substring(1).ToLower();
    
            // 🚀 NEW: Deep Link Interceptor
            // If command is /start and has a payload, use the payload as the command
            if (command == "start" && parts.Length > 1)
            {
                command = parts[1].ToLower();
            }

            // Now look up the target workflow based on the command (or deep link payload)
            if (_triggerMap.TryGetValue(command, out var targetWorkflow))
            {
                if (!await CheckAccessAsync(targetWorkflow, userId, update, ct)) return true;

                _userSessions.TryRemove(userId, out _);
                await StartNewSessionAsync(targetWorkflow, userId, currentChatId, update, ct);
                return true;
            }
    
            return false;
        }

        // 2. CHECK ACTIVE SESSION
        if (_userSessions.TryGetValue(userId, out var session))
        {
            // 🚀 【跨群組放行機制 (Session Trap Fix)】 🚀
            // 如果這是一條來自「其他群組」的普通文字訊息，我們不應該攔截它，直接放行給後方的 MessageRegistry！
            if (update.Type == UpdateType.Message && currentChatId != session.Context.ChatId && session.Context.ChatId != 0)
            {
                return false; 
            }

            if (_workflowMap.TryGetValue(session.WorkflowName, out var activeWorkflow))
            {
                if (!await CheckAccessAsync(activeWorkflow, userId, update, ct)) 
                {
                    _userSessions.TryRemove(userId, out _);
                    return true;
                }

                var nextState = await activeWorkflow.ExecuteStepAsync(_bot, update, session.Context, ct);

                if (nextState == null)
                    _userSessions.TryRemove(userId, out _);
                else
                    session.Context.CurrentState = nextState;
                
                return true;
            }
        }

        // 3. FALLBACK ENTRY POINTS (Non-Command triggers like Callback Buttons)
        foreach (var workflow in _workflowMap.Values)
        {
            if (workflow.IsEntryPoint(update))
            {
                if (!await CheckAccessAsync(workflow, userId, update, ct)) return true;

                await StartNewSessionAsync(workflow, userId, currentChatId, update, ct);
                return true;
            }
        }

        return false;
    }
    
    // --- Access Control Helper ---
    private async Task<bool> CheckAccessAsync(IConversation workflow, long userId, Update update, CancellationToken ct)
    {
        var attr = workflow.GetType().GetCustomAttribute<ConversationAttribute>();
        if (attr != null && attr.DevOnly && !_devIds.Contains(userId))
        {
            try 
            {
                if (update.CallbackQuery != null)
                {
                    await _bot.AnswerCallbackQuery(update.CallbackQuery.Id, "🚫 You are not the Dev!", showAlert: true, cancellationToken: ct);
                }
                else if (update.Message != null)
                {
                    await _bot.SendMessage(
                        chatId: update.Message.Chat.Id, 
                        text: "🚫 You are not the Dev!", 
                        replyParameters: new ReplyParameters { MessageId = update.Message.MessageId }, 
                        cancellationToken: ct);
                }
            }
            catch { }
            return false;
        }
        return true;
    }

    private async Task StartNewSessionAsync(IConversation workflow, long userId, long chatId, Update update, CancellationToken ct)
    {
        var context = new ConversationContext 
        { 
            CurrentState = "Start",
            ChatId = chatId // 記錄開啟 Session 時所在的 ChatId
        };
        
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