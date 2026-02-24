using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mud9Bot.Data.Entities;
using Mud9Bot.Data.Interfaces;

namespace Mud9Bot.Data.Interfaces;

public interface IBlacklistService
{
    Task InitializeAsync();
    bool IsBlacklisted(long telegramId);
    Task AddAsync(long telegramId, string reason, long adminId);
    Task RemoveAsync(long telegramId);
}