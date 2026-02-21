namespace Mud9Bot.Interfaces;

public interface ICangjieService
{
    /// <summary>
    /// 初始化並從 Data 資料夾載入所有 JSON 數據
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 取得單個中文字的倉頡碼與字根組成的字串
    /// </summary>
    (string Code, string Radicals)? GetCode(char c);
}