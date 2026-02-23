namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class CallbackQueryAttribute : Attribute
{
    public CallbackQueryAttribute(string prefix)
    {
        Prefix = prefix;
    }
    public string Prefix { get; set; }
    public bool DevOnly { get; set; } = false;
}