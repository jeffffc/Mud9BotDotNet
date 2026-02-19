namespace Mud9Bot.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class QuartzJobAttribute : Attribute
{
    public required string Name { get; set; }
    public string Group { get; set; } = "Default";
    public int IntervalSeconds { get; set; } = 60; // Default 1 minute
    public string CronInterval { get; set; } = ""; // e.g. "0 0 0 * * ?"
    public string Description { get; set; } = string.Empty;
    public bool Inactive { get; set; } = false;
    public bool RunOnStartup { get; set; } = false;
    
}