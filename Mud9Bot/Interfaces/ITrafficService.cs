namespace Mud9Bot.Interfaces;

public interface ITrafficService
{
    Task<string> GetTrafficNewsAsync(CancellationToken ct = default);
}