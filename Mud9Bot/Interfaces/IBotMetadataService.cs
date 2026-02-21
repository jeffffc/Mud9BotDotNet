namespace Mud9Bot.Interfaces;

public interface IBotMetadataService
{
    int CommandCount { get; set; }
    int CallbackCount { get; set; }
    int JobCount { get; set; }
    int ServiceCount { get; set; }
    int ConversationCount { get; set; }
    int MessageTriggerCount { get; set; }
}
