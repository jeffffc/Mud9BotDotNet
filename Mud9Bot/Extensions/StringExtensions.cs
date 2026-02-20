using Telegram.Bot.Extensions;

namespace Mud9Bot.Extensions;

public static class StringExtensions
{
    public static string EscapeMarkdown(this string? text)
    {
        return Markdown.Escape(text) ?? "";
    }

    public static string EscapeHtml(this string? text)
    {
        return HtmlText.Escape(text) ?? "";
    }

    public static string GetAny(this string[] enumerable)
    {
        return Random.Shared.GetItems(enumerable.AsSpan(), 1)[0];
    }
}