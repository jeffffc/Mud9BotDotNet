namespace Mud9Bot.Extensions;

public static class GeneralExtensions
{
    public static DateTime ToHkTime(this DateTime utc) 
        => utc.AddHours(8); // Simple version

    public static DateTime GetHkToday() 
        => DateTime.UtcNow.AddHours(8).Date;
}