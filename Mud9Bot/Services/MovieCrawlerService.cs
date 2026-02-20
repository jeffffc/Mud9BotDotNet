using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data.Entities;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;

namespace Mud9Bot.Services;

public interface IMovieCrawlerService
{
    Task<List<Movie>> FetchCurrentMoviesAsync();
}

public class MovieCrawlerService(ILogger<MovieCrawlerService> logger) : IMovieCrawlerService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<List<Movie>> FetchCurrentMoviesAsync()
    {
        var movies = new List<Movie>();
        try
        {
            string url = "https://wmoov.com/movie/showing";
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var movieNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'each')]");
            if (movieNodes == null) return movies;

            foreach (var node in movieNodes.Take(12)) 
            {
                if (movies.Count >= 6) break;

                try
                {
                    var titleNode = node.SelectSingleNode(".//a[@title]");
                    var title = titleNode?.GetAttributeValue("title", "") ?? "";

                    if (string.IsNullOrWhiteSpace(title) || title == "Unknown") continue;

                    var ratingNode = node.SelectSingleNode(".//div[contains(@class, 'rating')]//b") 
                                  ?? node.SelectSingleNode(".//div[contains(@class, 'rating')]");
                    
                    string rating = "--";
                    if (ratingNode != null)
                    {
                        var match = Regex.Match(ratingNode.InnerText, @"\d+(\.\d+)?");
                        if (match.Success) rating = match.Value;
                    }

                    var movie = new Movie
                    {
                        Title = title,
                        Link = "https://wmoov.com" + titleNode?.GetAttributeValue("href", ""),
                        Rating = rating,
                        IsShowing = true,
                        LastUpdated = DateTime.UtcNow
                    };

                    await FetchMovieDetails(movie);
                    movies.Add(movie);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse a movie entry.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scrape movies from wmoov.");
        }

        return movies;
    }

    private async Task FetchMovieDetails(Movie movie)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(movie.Link);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var descNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'movie-desc')]") 
                        ?? doc.DocumentNode.SelectSingleNode("//p[contains(@class, 'movie-desc')]")
                        ?? doc.DocumentNode.SelectSingleNode("//div[@id='movie-desc']");
            
            movie.Description = WebUtility.HtmlDecode(descNode?.InnerText ?? "").Replace("\r", "").Replace("\n", "").Trim();

            var infoDts = doc.DocumentNode.SelectNodes("//*[contains(@class, 'movie_info')]//dt");
            if (infoDts != null)
            {
                foreach (var dt in infoDts)
                {
                    string label = dt.InnerText.Trim();
                    var dd = dt.SelectSingleNode("following-sibling::dd[1]");
                    if (dd == null) continue;
                    
                    string value = WebUtility.HtmlDecode(dd.InnerText.Trim());

                    if (label.Contains("片種")) movie.Genre = value;
                    else if (label.Contains("導演")) movie.Director = value;
                    else if (label.Contains("主演")) movie.Starring = value;
                    else if (label.Contains("片長")) movie.Length = value;
                    else if (label.Contains("級別")) movie.Grade = value;
                    else if (label.Contains("語言")) movie.Language = value;
                    else if (label.Contains("編劇")) movie.Writer = value;
                    else if (label.Contains("上映"))
                    {
                        // 處理上映日期：移除換行並改用 / 作為分隔符號，將多行內容聚合為單行
                        var lines = value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        movie.OnShowDate = string.Join(" / ", lines.Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch details for movie: {Title}", movie.Title);
        }
    }
}