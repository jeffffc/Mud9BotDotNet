namespace Mud9Bot.Extensions;

public static class StringExtensions
{
    public static string EscapeMarkdown(this string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // Characters that must be escaped in MarkdownV2
        char[] specialChars = ['_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!'];
        foreach (var c in specialChars)
        {
            text = text.Replace(c.ToString(), "\\" + c);
        }
        return text;
    }

    public static string GetAny(this string[] enumerable)
    {
        return Random.Shared.GetItems(enumerable.AsSpan(), 1)[0];
    }
}