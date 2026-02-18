using Telegram.Bot.Types;

namespace Mud9Bot.Interfaces;

public interface IErrorReporter
{
    Task ReportErrorAsync(Exception exception, Message? message, CancellationToken ct = default);
}