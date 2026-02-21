using Mud9Bot.Interfaces;
using ChineseCharacterIdentifier;

namespace Mud9Bot.Services;

public class SimplifiedChineseService : ISimplifiedChineseService
{
    /// <summary>
    /// 封裝外部靜態類別 ChineseCharacterIdentifier 的識別方法
    /// </summary>
    public ChineseCharacterType Identify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ChineseCharacterType.None;
        
        // 呼叫你的 dotnet 函式庫靜態方法
        return ChinCharIdentifier.Identify(text);
    }

    /// <summary>
    /// 判定邏輯：只要結果包含純簡體字元 (Simplified 或 Both)，即視為攔截對象
    /// </summary>
    public bool IsSimplified(string text)
    {
        var result = Identify(text);
        
        // 根據定義：
        // Simplified: 包含純簡體字
        // Both: 包含純繁體也包含純簡體字
        // 這兩種情況都視為觸發「殘體字」警報
        return result == ChineseCharacterType.Simplified || result == ChineseCharacterType.Both;
    }
}