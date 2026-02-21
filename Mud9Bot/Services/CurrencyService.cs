using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Mud9Bot.Data;
using Mud9Bot.Data.Entities;
using Mud9Bot.Interfaces;
using Mud9Bot.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Mud9Bot.Services;

public class CurrencyService(
    IServiceScopeFactory scopeFactory, 
    ILogger<CurrencyService> logger, 
    IConfiguration config,
    HttpClient httpClient) : ICurrencyService
{
    private readonly ConcurrentDictionary<string, double> _hkdRatesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _apiKey = config["Fixer:ApiKey"] ?? "";
    private DateTime _lastUpdatedHk = DateTime.MinValue;

    // 貨幣別名對照表 - 將非標準輸入轉換為 Fixer 標準代號
    private static readonly Dictionary<string, string> CurrencyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "RMB", "CNY" },
        { "NTD", "TWD" },
        { "UKP", "GBP" },
        { "STG", "GBP" },
        { "RUR", "RUB" },
        { "GOLD", "XAU" },
        { "SILVER", "XAG" }
    };

    // 常用貨幣名稱對照表
    private static readonly Dictionary<string, string> CurrencyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "AED", "阿聯酋迪拉姆" }, { "AFN", "阿富汗尼" }, { "ALL", "阿爾巴尼亞列克" }, { "AMD", "亞美尼亞德拉姆" },
        { "ANG", "荷屬安地列斯盾" }, { "AOA", "安哥拉寬扎" }, { "ARS", "阿根廷披索" }, { "AUD", "澳元" },
        { "AWG", "阿魯巴弗羅林" }, { "AZN", "亞塞拜然馬納特" }, { "BAM", "波士尼亞馬克" }, { "BBD", "巴貝多元" },
        { "BDT", "孟加拉塔卡" }, { "BGN", "保加利亞列弗" }, { "BHD", "巴林第納爾" }, { "BIF", "蒲隆地法郎" },
        { "BMD", "百慕達元" }, { "BND", "汶萊元" }, { "BOB", "玻利維亞諾" }, { "BRL", "巴西雷亞爾" },
        { "BSD", "巴哈馬元" }, { "BTC", "比特幣" }, { "BTN", "不丹努爾特魯姆" }, { "BWP", "波札那普拉" },
        { "BYN", "白俄羅斯盧布" }, { "BZD", "貝里斯元" }, { "CAD", "加元" }, { "CDF", "剛果法郎" },
        { "CHF", "瑞士法郎" }, { "CLP", "智利披索" }, { "CNY", "人民幣" }, { "CNH", "離岸人民幣" },
        { "COP", "哥倫比亞披索" }, { "CRC", "哥斯大黎加科朗" }, { "CUC", "古巴轉帳披索" }, { "CUP", "古巴披索" },
        { "CVE", "佛德角埃斯庫多" }, { "CZK", "捷克克朗" }, { "DJF", "吉布地法郎" }, { "DKK", "丹麥克朗" },
        { "DOP", "多明尼加披索" }, { "DZD", "阿爾及利亞第納爾" }, { "EGP", "埃及鎊" }, { "ERN", "厄利垂亞納克法" },
        { "ETB", "衣索比亞比爾" }, { "EUR", "歐元" }, { "FJD", "斐濟元" }, { "FKP", "福克蘭鎊" },
        { "GBP", "英鎊" }, { "GEL", "拉里" }, { "GHS", "塞地" }, { "GIP", "直布羅陀鎊" },
        { "GMD", "達拉西" }, { "GNF", "幾內亞法郎" }, { "GTQ", "格查爾" }, { "GYD", "蓋亞那元" },
        { "HKD", "港幣" }, { "HNL", "倫皮拉" }, { "HRK", "庫納" }, { "HTG", "古德" },
        { "HUF", "福林" }, { "IDR", "印尼盾" }, { "ILS", "以色列新謝克爾" }, { "INR", "印度盧比" },
        { "IQD", "伊拉克第納爾" }, { "IRR", "伊朗里亞爾" }, { "ISK", "冰島克朗" }, { "JMD", "牙買加元" },
        { "JOD", "約旦第納爾" }, { "JPY", "日元" }, { "KES", "肯亞先令" }, { "KGS", "吉爾吉斯索姆" },
        { "KHR", "柬埔寨瑞爾" }, { "KMF", "葛摩法郎" }, { "KPW", "北韓圓" }, { "KRW", "韓圓" },
        { "KWD", "科威特第納爾" }, { "KYD", "開曼群島元" }, { "KZT", "堅戈" }, { "LAK", "基普" },
        { "LBP", "黎巴嫩鎊" }, { "LKR", "斯里蘭卡盧比" }, { "LRD", "賴比瑞亞元" }, { "LSL", "洛提" },
        { "LYD", "利比亞第納爾" }, { "MAD", "摩洛哥迪拉姆" }, { "MDL", "摩爾多瓦列伊" }, { "MGA", "馬達加斯加阿里亞里" },
        { "MKD", "馬其頓第納爾" }, { "MMK", "緬甸元" }, { "MNT", "圖格里克" }, { "MOP", "澳門幣" },
        { "MRU", "烏吉亞" }, { "MUR", "模里西斯盧比" }, { "MVR", "羅非亞" }, { "MWK", "誇查" },
        { "MXN", "墨西哥披索" }, { "MYR", "馬幣" }, { "MZN", "梅蒂卡爾" }, { "NAD", "納米比亞元" },
        { "NGN", "奈拉" }, { "NIO", "科多巴" }, { "NOK", "挪威克朗" }, { "NPR", "尼泊爾盧比" },
        { "NZD", "紐西蘭元" }, { "OMR", "奧曼里亞爾" }, { "PAB", "巴波亞" }, { "PEN", "索爾" },
        { "PGK", "基那" }, { "PHP", "披索" }, { "PKR", "巴基斯坦盧比" }, { "PLN", "茲羅提" },
        { "PYG", "瓜拉尼" }, { "QAR", "卡達里亞爾" }, { "RON", "羅馬尼亞列伊" }, { "RSD", "塞爾維亞第納爾" },
        { "RUB", "俄羅斯盧布" }, { "RWF", "盧安達法郎" }, { "SAR", "沙烏地里亞爾" }, { "SBD", "所羅門群島元" },
        { "SCR", "塞席爾盧比" }, { "SDG", "蘇丹鎊" }, { "SEK", "瑞典克朗" }, { "SGD", "坡幣" },
        { "SHP", "聖赫勒拿鎊" }, { "SLE", "塞拉利昂利昂" }, { "SLL", "利昂" }, { "SOS", "索馬利亞先令" },
        { "SRD", "蘇利南元" }, { "STD", "聖多美多布拉" }, { "STN", "聖多美多布拉(新)" }, { "SVC", "薩爾瓦多科朗" },
        { "SYP", "敘利亞鎊" }, { "SZL", "里蘭吉尼" }, { "THB", "泰銖" }, { "TJS", "索莫尼" },
        { "TMT", "馬納特" }, { "TND", "突尼西亞第納爾" }, { "TOP", "潘加" }, { "TRY", "土耳其里拉" },
        { "TTD", "千里達托貝哥元" }, { "TWD", "台幣" }, { "TZS", "坦尚尼亞先令" }, { "UAH", "荷林夫納" },
        { "UGX", "烏干達先令" }, { "USD", "美元" }, { "UYU", "烏拉圭披索" }, { "UZS", "烏茲別克索姆" },
        { "VES", "玻利瓦" }, { "VND", "越南盾" }, { "VUV", "瓦圖" }, { "WST", "塔拉" },
        { "XAF", "中非法郎" }, { "XAG", "白銀" }, { "XAU", "黃金" }, { "XCD", "東加勒比元" },
        { "XDR", "特別提款權" }, { "XOF", "西非法郎" }, { "XPF", "太平洋法郎" }, { "YER", "葉門里亞爾" },
        { "ZAR", "南非蘭特" }, { "ZMW", "尚比亞誇查" }, { "ZWL", "津巴布韋元" }
    };

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            var rates = await db.Set<CurrencyRate>().ToListAsync();
            
            _hkdRatesCache.Clear();
            DateTime maxUpdated = DateTime.MinValue;

            foreach (var r in rates)
            {
                _hkdRatesCache[r.Code] = r.RateHkd;
                if (r.LastUpdated > maxUpdated) maxUpdated = r.LastUpdated;
            }

            _lastUpdatedHk = maxUpdated.ToHkTime();

            logger.LogInformation("Currency RAM cache primed with {Count} HKD-based rates. (Last Sync: {Time})", 
                _hkdRatesCache.Count, _lastUpdatedHk);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Currency cache.");
        }
    }

    public async Task UpdateRatesFromApiAsync()
    {
        await InitializeAsync();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            
            var todayHk = DateTime.UtcNow.ToHkTime().Date;
            var latestRecord = await db.Set<CurrencyRate>().OrderByDescending(r => r.LastUpdated).FirstOrDefaultAsync();
            
            if (latestRecord != null && latestRecord.LastUpdated.ToHkTime().Date == todayHk)
            {
                logger.LogInformation("匯率數據今日 ({Date}) 已存在，跳過 API 抓取。", todayHk.ToString("yyyy-MM-dd"));
                return;
            }
        }
        catch (Exception ex) { logger.LogError(ex, "Check DB failed."); }

        if (string.IsNullOrEmpty(_apiKey)) return;

        try
        {
            logger.LogInformation("正在從 Fixer API 抓取最新匯率...");
            string url = $"https://data.fixer.io/api/latest?access_key={_apiKey}";
            var result = await httpClient.GetFromJsonAsync<FixerResponse>(url);

            if (result == null || !result.Success || result.Rates == null || !result.Rates.TryGetValue("HKD", out double eurToHkdRate))
            {
                logger.LogWarning("Fixer API 返回無效數據。");
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            
            var existingRates = await db.Set<CurrencyRate>().ToDictionaryAsync(r => r.Code, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var kvp in result.Rates)
            {
                var code = kvp.Key.ToUpper();
                double rateEur = kvp.Value;
                double rateHkd = rateEur / eurToHkdRate;

                _hkdRatesCache[code] = rateHkd;

                if (existingRates.TryGetValue(code, out var existing))
                {
                    existing.Rate = rateEur;
                    existing.RateHkd = rateHkd;
                    existing.LastUpdated = now;
                }
                else
                {
                    db.Set<CurrencyRate>().Add(new CurrencyRate 
                    { 
                        Code = code, 
                        Rate = rateEur, 
                        RateHkd = rateHkd, 
                        LastUpdated = now 
                    });
                }
            }

            await db.SaveChangesAsync();
            _lastUpdatedHk = now.ToHkTime();
            logger.LogInformation("批量匯率同步完成。");
        }
        catch (Exception ex) { logger.LogError(ex, "API Update failed."); }
    }

    public ConversionResult Convert(double amount, string fromCode, string toCode)
    {
        // 別名解析邏輯
        string resolvedFrom = CurrencyAliases.GetValueOrDefault(fromCode, fromCode.ToUpper());
        string resolvedTo = CurrencyAliases.GetValueOrDefault(toCode, toCode.ToUpper());

        if (!_hkdRatesCache.TryGetValue(resolvedFrom, out double fromHkdRate))
            return new ConversionResult(false, $"搵唔到 <code>{resolvedFrom}</code> 嘅匯率資料。");

        if (!_hkdRatesCache.TryGetValue(resolvedTo, out double toHkdRate))
            return new ConversionResult(false, $"搵唔到 <code>{resolvedTo}</code> 嘅匯率資料。");

        double result = amount * (toHkdRate / fromHkdRate);
        
        string fromName = CurrencyNames.GetValueOrDefault(resolvedFrom, resolvedFrom);
        string toName = CurrencyNames.GetValueOrDefault(resolvedTo, resolvedTo);

        string timeStr = _lastUpdatedHk == DateTime.MinValue ? "未知" : _lastUpdatedHk.ToString("yyyy-MM-dd HH:mm");
        
        // 格式化輸出，備註部分改為全等寬字體並移除超連結
        string msg = $"<code>{amount:N2} {fromName}({resolvedFrom})</code> = <code>{result:N3} {toName}({resolvedTo})</code>\n\n" +
                     $"<code>(匯率更新於：{timeStr} | 來源：fixer.io)</code>";
                     
        return new ConversionResult(true, msg);
    }

    private class FixerResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("rates")] public Dictionary<string, double>? Rates { get; set; }
    }
}