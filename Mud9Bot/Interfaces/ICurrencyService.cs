namespace Mud9Bot.Interfaces;

public record ConversionResult(bool Success, string Message);

public interface ICurrencyService
{
    /// <summary>
    /// 從資料庫載入所有匯率到記憶體中
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 從 Fixer API 抓取最新匯率並同步至資料庫與快取
    /// </summary>
    Task UpdateRatesFromApiAsync();

    /// <summary>
    /// 執行匯率轉換
    /// </summary>
    ConversionResult Convert(double amount, string fromCode, string toCode);
}