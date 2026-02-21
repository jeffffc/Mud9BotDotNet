using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;
using Mud9Bot.Models;

namespace Mud9Bot.Services;

public class NewsService(HttpClient httpClient, ILogger<NewsService> logger) : INewsService
{
    private readonly ConcurrentDictionary<NewsCategory, List<NewsArticle>> _newsCache = new();

    private static readonly Dictionary<NewsCategory, string> RssUrls = new()
    {
        // 🚀 Reverted to official stable URLs. Redirection will be handled automatically.
        { NewsCategory.Local, "https://rthk.hk/rthk/news/rss/c_expressnews_clocal.xml" },
        { NewsCategory.GreaterChina, "https://rthk.hk/rthk/news/rss/c_expressnews_greaterchina.xml" },
        { NewsCategory.International, "https://rthk.hk/rthk/news/rss/c_expressnews_cinternational.xml" },
        { NewsCategory.Finance, "https://rthk.hk/rthk/news/rss/c_expressnews_cfinance.xml" },
        { NewsCategory.Sports, "https://rthk.hk/rthk/news/rss/c_expressnews_csport.xml" }
    };

    public List<NewsArticle> GetNews(NewsCategory category) 
        => _newsCache.GetValueOrDefault(category) ?? new List<NewsArticle>();

    public async Task UpdateAllNewsAsync(CancellationToken ct = default)
    {
        foreach (var kvp in RssUrls)
        {
            try
            {
                var articles = await FetchRssFeedAsync(kvp.Value, ct);
                if (articles.Any())
                {
                    _newsCache[kvp.Key] = articles.Take(5).ToList();
                    logger.LogInformation("Updated news for {Category}: {Count} articles.", kvp.Key, _newsCache[kvp.Key].Count);
                }
                else
                {
                    logger.LogWarning("Fetch returned 0 articles for {Category}.", kvp.Key);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update news for category {Category}", kvp.Key);
            }
        }
    }

    private async Task<List<NewsArticle>> FetchRssFeedAsync(string url, CancellationToken ct)
    {
        var articles = new List<NewsArticle>();
        string currentUrl = url;
        int maxRedirects = 3;

        for (int i = 0; i < maxRedirects; i++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/xml, application/xml, application/rss+xml, */*");
                request.Headers.Add("Accept-Language", "zh-TW,zh;q=0.9,en-US;q=0.8");

                var response = await httpClient.SendAsync(request, ct);

                // 🚀 Handle Redirection manually if HttpClient is configured to not follow them automatically
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.TemporaryRedirect or (HttpStatusCode)308)
                {
                    var newLocation = response.Headers.Location;
                    if (newLocation != null)
                    {
                        currentUrl = newLocation.IsAbsoluteUri ? newLocation.ToString() : new Uri(new Uri(currentUrl), newLocation).ToString();
                        logger.LogDebug("Following redirect to: {NewUrl}", currentUrl);
                        continue;
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Fetch failed for {Url}: HTTP {Status}", currentUrl, response.StatusCode);
                    return articles;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var doc = XDocument.Load(stream);

                var items = doc.Descendants().Where(e => e.Name.LocalName == "item");

                foreach (var item in items)
                {
                    var titleNode = item.Elements().FirstOrDefault(e => e.Name.LocalName == "title");
                    var linkNode = item.Elements().FirstOrDefault(e => e.Name.LocalName == "link");
                    var descNode = item.Elements().FirstOrDefault(e => e.Name.LocalName == "description");
                    var dateNode = item.Elements().FirstOrDefault(e => e.Name.LocalName == "pubDate");

                    var article = new NewsArticle
                    {
                        Title = titleNode?.Value ?? "無標題",
                        Link = linkNode?.Value ?? "",
                        Description = WebUtility.HtmlDecode(descNode?.Value ?? "").Trim(),
                        PubDate = DateTime.TryParse(dateNode?.Value, out var dt) ? dt : DateTime.MinValue
                    };

                    if (!string.IsNullOrEmpty(article.Description))
                    {
                        string cleanDesc = System.Text.RegularExpressions.Regex.Replace(article.Description, "<.*?>", string.Empty);
                        article.Description = System.Text.RegularExpressions.Regex.Replace(cleanDesc, @"\s+", " ").Trim();
                    }
                    
                    articles.Add(article);
                }

                return articles.OrderByDescending(a => a.PubDate).ToList();
            }
            catch (System.Xml.XmlException xmlEx)
            {
                logger.LogError(xmlEx, "XML Parsing error for {Url}", currentUrl);
                return articles;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error fetching RSS from {Url}", currentUrl);
                return articles;
            }
        }

        logger.LogWarning("Exceeded maximum redirects for {Url}", url);
        return articles;
    }
}