namespace Mud9Bot.Interfaces;
using ChineseCharacterIdentifier;

/// <summary>
/// 識別結果列舉，與外部 .NET 函式庫定義一致
/// </summary>
public interface ISimplifiedChineseService
{
    /// <summary>
    /// 檢查字串是否被識別為包含簡體字元
    /// </summary>
    bool IsSimplified(string text);

    /// <summary>
    /// 獲取詳細的識別結果
    /// </summary>
    ChineseCharacterType Identify(string text);
}