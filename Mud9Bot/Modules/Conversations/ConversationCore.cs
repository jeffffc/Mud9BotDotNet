using System.Collections.Concurrent;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;

namespace Mud9Bot.Modules.Conversations;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConversationAttribute : Attribute
{
    public string Trigger { get; }
    public string Description { get; set; } = "";
    public bool DevOnly { get; set; } = false;
    
    public ConversationAttribute(string trigger) => Trigger = trigger.ToLower();
}

public interface IConversation
{
    string ConversationName { get; }
    // 用於識別按鈕數據是否屬於此對話 (例如 data.StartsWith("HELP+"))
    bool IsEntryPoint(Update update) => false;
    Task<string?> ExecuteStepAsync(ITelegramBotClient bot, Update update, ConversationContext context, CancellationToken ct);
}

public class ConversationContext
{
    public string CurrentState { get; set; } = "Start";
    public int MenuMessageId { get; set; } 
    public long ChatId { get; set; } // 紀錄會話發生的 Chat
    public Dictionary<string, object> Data { get; set; } = new();
}

public class ConversationManager
{
    private readonly ITelegramBotClient _bot;
    private readonly Dictionary<string, IConversation> _triggerMap = new();
    private readonly List<IConversation> _allConversations = new();
    
    // 僅用於追蹤「正在等待文字輸入」的會話
    private readonly ConcurrentDictionary<long, (string WorkflowName, ConversationContext Context)> _activeInputSessions = new();
    private readonly HashSet<long> _devIds;
    
    // 🚀 新增：供外部檢查指令是否為有效的會話觸發詞
    public bool HasTrigger(string command) => _triggerMap.ContainsKey(command.ToLower());

    public ConversationManager(ITelegramBotClient bot, IEnumerable<IConversation> conversations, IConfiguration config)
    {
        _bot = bot;
        _devIds = config.GetSection("BotConfiguration:DevIds").Get<HashSet<long>>() ?? new HashSet<long>();
        
        foreach (var conv in conversations)
        {
            _allConversations.Add(conv);
            var attr = conv.GetType().GetCustomAttribute<ConversationAttribute>();
            if (attr != null) _triggerMap[attr.Trigger] = conv;
        }
    }

    public async Task<bool> HandleUpdateAsync(Update update, CancellationToken ct)
    {
        var user = update.Message?.From ?? update.CallbackQuery?.From;
        if (user == null) return false;
        long userId = user.Id;

        // ---------------------------------------------------------
        // 1. 處理指令 (優先級最高)
        // ---------------------------------------------------------
        if (update.Message?.Text is { } text && text.StartsWith("/"))
        {
            var parts = text.Split(' ', 2);
            string rawCommand = parts[0].Substring(1); // 取得 '/' 後的內容

            // 🚀 解析 @BotName 尾綴
            int atIndex = rawCommand.IndexOf('@');
            string command = (atIndex > 0 ? rawCommand.Substring(0, atIndex) : rawCommand).ToLower();

            // Deep Link 支援
            if (command == "start" && parts.Length > 1)
                command = parts[1].ToLower();

            // 如果是會話觸發詞（開啟新對話或重新開始）
            if (_triggerMap.TryGetValue(command, out var targetWorkflow))
            {
                if (!await CheckAccessAsync(targetWorkflow, userId, update, ct)) return true;
                
                // 開始新對話前，清除該使用者舊的輸入鎖定
                _activeInputSessions.TryRemove(userId, out _);
                await StartWorkflowAsync(targetWorkflow, userId, update, ct);
                return true;
            }
            
            // 🚀 關鍵修正：
            // 如果指令不是會話觸發詞（例如 /cancel），且使用者目前「沒有」處於文字輸入鎖定狀態，
            // 則視為普通指令，清除潛在 session 並放行給 CommandRegistry。
            // 但如果使用者「正在」輸入鎖定中（例如 AwaitingAddMorning），則不在此處移除，
            // 讓邏輯流向後面的 Part 3，由對話本身來決定如何處理該指令。
            if (!_activeInputSessions.ContainsKey(userId))
            {
                return false;
            }
        }

        // ---------------------------------------------------------
        // 2. 處理按鈕 (無狀態路由：不論有沒有 session，只要前綴對了就處理)
        // ---------------------------------------------------------
        if (update.Type == UpdateType.CallbackQuery)
        {
            foreach (var conv in _allConversations)
            {
                if (conv.IsEntryPoint(update))
                {
                    if (!await CheckAccessAsync(conv, userId, update, ct)) return true;

                    // 嘗試抓取現有的 Context (如果有的話)，否則建立新的
                    var context = _activeInputSessions.TryGetValue(userId, out var session) && session.WorkflowName == conv.ConversationName
                        ? session.Context 
                        : new ConversationContext { CurrentState = "Menu" };

                    var nextState = await conv.ExecuteStepAsync(_bot, update, context, ct);
                    
                    UpdateSession(userId, conv.ConversationName, context, nextState);
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------
        // 3. 處理文字輸入鎖定 (只有當狀態不是 Start/Menu 時才攔截)
        // ---------------------------------------------------------
        if (_activeInputSessions.TryGetValue(userId, out var active))
        {
            // 如果狀態是 Start 或 Menu，代表對話處於「閒置/選單」模式，不應攔截普通文字
            if (active.Context.CurrentState == "Start" || active.Context.CurrentState == "Menu")
            {
                return false; 
            }

            var workflow = _allConversations.FirstOrDefault(c => c.ConversationName == active.WorkflowName);
            if (workflow != null)
            {
                var nextState = await workflow.ExecuteStepAsync(_bot, update, active.Context, ct);
                UpdateSession(userId, active.WorkflowName, active.Context, nextState);
                return true;
            }
        }

        return false;
    }

    private void UpdateSession(long userId, string workflowName, ConversationContext context, string? nextState)
    {
        if (string.IsNullOrEmpty(nextState) || nextState == "Start" || nextState == "Menu")
        {
            // 如果對話結束或回到選單，釋放文字輸入鎖定
            _activeInputSessions.TryRemove(userId, out _);
        }
        else
        {
            // 否則，持續鎖定該使用者的文字輸入
            context.CurrentState = nextState;
            _activeInputSessions[userId] = (workflowName, context);
        }
    }

    private async Task StartWorkflowAsync(IConversation workflow, long userId, Update update, CancellationToken ct)
    {
        var context = new ConversationContext { CurrentState = "Start" };
        var nextState = await workflow.ExecuteStepAsync(_bot, update, context, ct);
        UpdateSession(userId, workflow.ConversationName, context, nextState);
    }

    private async Task<bool> CheckAccessAsync(IConversation workflow, long userId, Update update, CancellationToken ct)
    {
        var attr = workflow.GetType().GetCustomAttribute<ConversationAttribute>();
        if (attr != null && attr.DevOnly && !_devIds.Contains(userId))
        {
            try {
                if (update.CallbackQuery != null)
                    await _bot.AnswerCallbackQuery(update.CallbackQuery.Id, "🚫 你無權使用此開發者功能。", showAlert: true, cancellationToken: ct);
                else
                    await _bot.SendMessage(update.Message!.Chat.Id, "🚫 你無權使用此開發者功能。", cancellationToken: ct);
            } catch { }
            return false;
        }
        return true;
    }
}