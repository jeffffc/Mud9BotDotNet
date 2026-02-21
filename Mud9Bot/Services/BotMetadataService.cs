using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;


public class BotMetadataService : IBotMetadataService
{
    public int CommandCount { get; set; }
    public int CallbackCount { get; set; }
    public int JobCount { get; set; }
    public int ServiceCount { get; set; }
    public int ConversationCount { get; set; }
    public int MessageTriggerCount { get; set; }
}