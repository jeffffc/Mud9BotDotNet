namespace Mud9Bot.Services.Interfaces;

public interface ITrafficService
{
    Task<string> GetTrafficNewsAsync(CancellationToken ct = default);
}