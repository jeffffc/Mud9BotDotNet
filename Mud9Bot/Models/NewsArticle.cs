namespace Mud9Bot.Models;

public class NewsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime PubDate { get; set; }
}

public enum NewsCategory
{
    Local,
    GreaterChina,
    International,
    Finance,
    Sports
}