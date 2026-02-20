using Mud9Bot.Data.Entities;

namespace Mud9Bot.Interfaces;

public interface IMovieCrawlerService
{
    Task<List<Movie>> FetchCurrentMoviesAsync();
}
