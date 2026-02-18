namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class CallbackQueryAttribute(string prefix) : Attribute
{
    public string Prefix { get; } = prefix;
}