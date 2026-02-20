using Mud9Bot.Data.Entities;

namespace Mud9Bot.Interfaces;

public interface IMovieService
{
    List<Movie> GetCachedMovies();
    Task UpdateMoviesAsync(List<Movie> scrapedMovies);
    Task InitializeAsync();
}