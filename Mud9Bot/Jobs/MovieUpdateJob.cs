using Mud9Bot.Attributes;
using Mud9Bot.Services;
using Quartz;

namespace Mud9Bot.Jobs;

[QuartzJob(Name = "MovieUpdateJob", CronInterval = "0 0 */2 * * ?", RunOnStartup = true, Description = "Fetch movies every 2 hours")]
public class MovieUpdateJob(IMovieService movieService, IMovieCrawlerService crawler, ILogger<MovieUpdateJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Movie Update Job starting...");
        var movies = await crawler.FetchCurrentMoviesAsync();
        
        if (movies.Any())
        {
            await movieService.UpdateMoviesAsync(movies);
        }
        else
        {
            logger.LogWarning("Scraper returned 0 movies, skipping update.");
        }
    }
}