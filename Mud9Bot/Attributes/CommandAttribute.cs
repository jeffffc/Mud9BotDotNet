namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CommandAttribute : Attribute
{
    public string[] Triggers { get; }
    public string Description { get; set; } = string.Empty;
    
    // 取第一個觸發詞作為主指令（用於幫助選單或日誌）
    public string PrimaryTrigger => Triggers.FirstOrDefault() ?? string.Empty;

    public CommandAttribute(params string[] triggers)
    {
        // 統一轉為小寫處理
        Triggers = triggers.Select(t => t.ToLower()).ToArray();
    }
    
    /// <summary>
    /// If true, command is globally disabled.
    /// </summary>
    public bool Inactive { get; set; } = false;

    /// <summary>
    /// If true, only group administrators can use this command.
    /// </summary>
    public bool AdminOnly { get; set; } = false;

    /// <summary>
    /// If true, only users listed in BotConfiguration:DevIds can use this command.
    /// </summary>
    public bool DevOnly { get; set; } = false;

    /// <summary>
    /// If true, command can only be used in private chats.
    /// </summary>
    public bool PrivateOnly { get; set; } = false;

    /// <summary>
    /// If true, command can only be used in group/supergroup chats.
    /// </summary>
    public bool GroupOnly { get; set; } = false;
}