namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CommandAttribute(string trigger) : Attribute
{
    public string Trigger { get; } = trigger;
    public string Description { get; set; } = string.Empty;
    
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