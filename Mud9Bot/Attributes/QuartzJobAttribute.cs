namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class QuartzJobAttribute : Attribute
{
    public string Name { get; set; }
    public string Group { get; set; } = "Default";
    public int IntervalSeconds { get; set; } = 60; // Default 1 minute
    public string Description { get; set; } = string.Empty;
    public bool Inactive { get; set; } = false;

    public QuartzJobAttribute(string name)
    {
        Name = name;
    }
}