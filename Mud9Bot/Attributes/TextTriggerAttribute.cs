using System.Text.RegularExpressions;

namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class TextTriggerAttribute : Attribute
{
    public string Pattern { get; }
    public string Description { get; set; } = string.Empty;
    
    public bool Inactive { get; set; } = false;
    public bool AdminOnly { get; set; } = false;
    public bool DevOnly { get; set; } = false;
    public bool PrivateOnly { get; set; } = false;
    public bool GroupOnly { get; set; } = false;

    /// <summary>
    /// Trigger a method when the incoming message text matches the given regex pattern.
    /// </summary>
    /// <param name="pattern">The regular expression pattern to match.</param>
    public TextTriggerAttribute(string pattern)
    {
        Pattern = pattern;
    }
}