using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Mud9Bot.Web.Services;

/// <summary>
/// Service to validate Telegram WebApp initData.
/// 驗證 Telegram WebApp 傳過嚟嘅資料係咪真係由 Telegram 發出，防止有人冒充。
/// </summary>
public class TelegramAuthService(IConfiguration config)
{
    private readonly string _botToken = config["BotToken"] ?? throw new ArgumentNullException("BotToken missing in config");

    public bool ValidateInitData(string initData)
    {
        // 1. Parse the query string
        var parsed = System.Web.HttpUtility.ParseQueryString(initData);
        var hash = parsed["hash"];
        if (string.IsNullOrEmpty(hash)) return false;

        // 2. Prepare data-check-string (sort alphabetically, exclude hash)
        // 按照字母順序排返好啲資料，除咗個 hash 之外
        var keys = parsed.AllKeys.Where(k => k != "hash").OrderBy(k => k).ToList();
        var checkString = string.Join("\n", keys.Select(k => $"{k}={parsed[k]}"));

        // 3. Generate Secret Key: HMAC-SHA256("WebAppData", BotToken)
        using var hmacSecret = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = hmacSecret.ComputeHash(Encoding.UTF8.GetBytes(_botToken));

        // 4. Generate Hash: HMAC-SHA256(SecretKey, data-check-string)
        using var hmacHash = new HMACSHA256(secretKey);
        var computedHashBytes = hmacHash.ComputeHash(Encoding.UTF8.GetBytes(checkString));
        var computedHash = BitConverter.ToString(computedHashBytes).Replace("-", "").ToLower();

        // 5. Compare computed vs received
        return computedHash == hash;
    }
}