using Mud9Bot.Transport.Models;

namespace Mud9Bot.Transport.Interfaces;

public interface IMtrApiService
{
    Task<MtrScheduleResponse?> GetScheduleAsync(string line, string station);
    List<MtrLineDto> GetLines();
    List<MtrStationDto> GetStationsForLine(string line);
}