using System.Net.Http.Json;
using Mud9Bot.Transport.Interfaces;
using Mud9Bot.Transport.Models;

namespace Mud9Bot.Transport.Services;

/// <summary>
/// Service to fetch data from the official MTR Next Train API.
/// 處理港鐵 API 請求嘅服務，包埋靜態嘅車站名單。
/// </summary>
public class MtrApiService(IHttpClientFactory httpClientFactory) : IMtrApiService
{
    private const string MtrApiUrl = "https://rt.data.gov.hk/v1/transport/mtr/getSchedule.php";

    public async Task<MtrScheduleResponse?> GetScheduleAsync(string line, string station)
    {
        var client = httpClientFactory.CreateClient();
        string url = $"{MtrApiUrl}?line={line.ToUpper()}&sta={station.ToUpper()}";

        try
        {
            return await client.GetFromJsonAsync<MtrScheduleResponse>(url);
        }
        catch
        {
            // Silently fail if MTR API is offline or returns invalid HTML
            return null;
        }
    }

    public List<MtrLineDto> GetLines()
    {
        return new List<MtrLineDto>
        {
            new("KTL", "觀塘綫", "Kwun Tong Line", "#00AB4E"),
            new("TWL", "荃灣綫", "Tsuen Wan Line", "#ED1D24"),
            new("ISL", "港島綫", "Island Line", "#0071CE"),
            new("TKL", "將軍澳綫", "Tseung Kwan O Line", "#7A288A"),
            new("TCL", "東涌綫", "Tung Chung Line", "#F68121"),
            new("AEL", "機場快綫", "Airport Express", "#00888A"),
            new("TML", "屯馬綫", "Tuen Ma Line", "#9A3B26"),
            new("EAL", "東鐵綫", "East Rail Line", "#53B7E8"),
            new("SIL", "南港島綫", "South Island Line", "#B5A879")
        };
    }

    public List<MtrStationDto> GetStationsForLine(string line)
    {
        // Because MTR doesn't offer a topology endpoint, we hardcode the station sequences.
        // 港鐵冇提供車站列表 API，所以要自己 Hardcode。你可以稍後慢慢補齊所有站。
        return line.ToUpper() switch
        {
            "KTL" => [ // 觀塘綫 Kwun Tong Line
                new("WHA", "黃埔", "Whampoa"), new("HOM", "何文田", "Ho Man Tin"),
                new("YMT", "油麻地", "Yau Ma Tei"), new("MOK", "旺角", "Mong Kok"),
                new("PRE", "太子", "Prince Edward"), new("SKM", "石硤尾", "Shek Kip Mei"),
                new("KOT", "九龍塘", "Kowloon Tong"), new("LOF", "樂富", "Lok Fu"),
                new("WTS", "黃大仙", "Wong Tai Sin"), new("CHH", "彩虹", "Choi Hung"),
                new("DIH", "鑽石山", "Diamond Hill"), new("KOB", "九龍灣", "Kowloon Bay"),
                new("NTK", "牛頭角", "Ngau Tau Kok"), new("KWT", "觀塘", "Kwun Tong"),
                new("LAT", "藍田", "Lam Tin"), new("YAT", "油塘", "Yau Tong"),
                new("TIK", "調景嶺", "Tiu Keng Leng")
            ],
            "TWL" => [ // 荃灣綫 Tsuen Wan Line
                new("CEN", "中環", "Central"), new("ADM", "金鐘", "Admiralty"),
                new("TST", "尖沙咀", "Tsim Sha Tsui"), new("JOR", "佐敦", "Jordan"),
                new("YMT", "油麻地", "Yau Ma Tei"), new("MOK", "旺角", "Mong Kok"),
                new("PRE", "太子", "Prince Edward"), new("SSP", "深水埗", "Sham Shui Po"),
                new("CSW", "長沙灣", "Cheung Sha Wan"), new("LCK", "荔枝角", "Lai Chi Kok"),
                new("MEF", "美孚", "Mei Foo"), new("LAK", "荔景", "Lai King"),
                new("KWF", "葵芳", "Kwai Fong"), new("KWH", "葵興", "Kwai Hing"),
                new("TWH", "大窩口", "Tai Wo Hau"), new("TSW", "荃灣", "Tsuen Wan")
            ],
            "ISL" => [ // 港島綫 Island Line
                new("KET", "堅尼地城", "Kennedy Town"), new("HKU", "香港大學", "HKU"),
                new("SYP", "西營盤", "Sai Ying Pun"), new("SHW", "上環", "Sheung Wan"),
                new("CEN", "中環", "Central"), new("ADM", "金鐘", "Admiralty"),
                new("WAC", "灣仔", "Wan Chai"), new("CAB", "銅鑼灣", "Causeway Bay"),
                new("TIN", "天后", "Tin Hau"), new("FOH", "炮台山", "Fortress Hill"),
                new("NOP", "北角", "North Point"), new("QUB", "鰂魚涌", "Quarry Bay"),
                new("TAK", "太古", "Tai Koo"), new("SWH", "西灣河", "Sai Wan Ho"),
                new("SKW", "筲箕灣", "Shau Kei Wan"), new("HFC", "杏花邨", "Heng Fa Chuen"),
                new("CHW", "柴灣", "Chai Wan")
            ],
            "TKL" => [ // 將軍澳綫 Tseung Kwan O Line
                new("NOP", "北角", "North Point"), new("QUB", "鰂魚涌", "Quarry Bay"),
                new("YAT", "油塘", "Yau Tong"), new("TIK", "調景嶺", "Tiu Keng Leng"),
                new("TKO", "將軍澳", "Tseung Kwan O"), new("HAH", "坑口", "Hang Hau"),
                new("POA", "寶琳", "Po Lam"), new("LHP", "康城", "LOHAS Park")
            ],
            "TCL" => [ // 東涌綫 Tung Chung Line
                new("HOK", "香港", "Hong Kong"), new("KOW", "九龍", "Kowloon"),
                new("OLY", "奧運", "Olympic"), new("NAC", "南昌", "Nam Cheong"),
                new("LAK", "荔景", "Lai King"), new("TSY", "青衣", "Tsing Yi"),
                new("SUN", "欣澳", "Sunny Bay"), new("TUC", "東涌", "Tung Chung")
            ],
            "AEL" => [ // 機場快綫 Airport Express
                new("HOK", "香港", "Hong Kong"), new("KOW", "九龍", "Kowloon"),
                new("TSY", "青衣", "Tsing Yi"), new("AIR", "機場", "Airport"),
                new("AWE", "博覽館", "AsiaWorld-Expo")
            ],
            "TML" => [ // 屯馬綫 Tuen Ma Line
                new("WKS", "烏溪沙", "Wu Kai Sha"), new("MOS", "馬鞍山", "Ma On Shan"),
                new("HEO", "恆安", "Heng On"), new("TSH", "大水坑", "Tai Shui Hang"),
                new("SHM", "石門", "Shek Mun"), new("CIO", "第一城", "City One"),
                new("STW", "沙田圍", "Sha Tin Wai"), new("CKT", "車公廟", "Che Kung Temple"),
                new("TAW", "大圍", "Tai Wai"), new("HIK", "顯徑", "Hin Keng"),
                new("DIH", "鑽石山", "Diamond Hill"), new("KAT", "啟德", "Kai Tak"),
                new("SUW", "宋皇臺", "Sung Wong Toi"), new("TKW", "土瓜灣", "To Kwa Wan"),
                new("HOM", "何文田", "Ho Man Tin"), new("HUH", "紅磡", "Hung Hom"),
                new("ETS", "尖東", "East Tsim Sha Tsui"), new("AUS", "柯士甸", "Austin"),
                new("NAM", "南昌", "Nam Cheong"), new("MEF", "美孚", "Mei Foo"),
                new("TWW", "荃灣西", "Tsuen Wan West"), new("KSR", "錦上路", "Kam Sheung Road"),
                new("YUL", "元朗", "Yuen Long"), new("LOP", "朗屏", "Long Ping"),
                new("TIS", "天水圍", "Tin Shui Wai"), new("SIH", "兆康", "Siu Hong"),
                new("TUM", "屯門", "Tuen Mun")
            ],
            "EAL" => [ // 東鐵綫 East Rail Line
                new("ADM", "金鐘", "Admiralty"), new("EXK", "會展", "Exhibition Centre"),
                new("HUH", "紅磡", "Hung Hom"), new("MKK", "旺角東", "Mong Kok East"),
                new("KOT", "九龍塘", "Kowloon Tong"), new("TAW", "大圍", "Tai Wai"),
                new("SHT", "沙田", "Sha Tin"), new("FOT", "火炭", "Fo Tan"),
                new("RAC", "馬場", "Racecourse"), new("UNI", "大學", "University"),
                new("TAP", "大埔墟", "Tai Po Market"), new("TWO", "太和", "Tai Wo"),
                new("FAN", "粉嶺", "Fanling"), new("SHS", "上水", "Sheung Shui"),
                new("LOW", "羅湖", "Lo Wu"), new("LMC", "落馬洲", "Lok Ma Chau")
            ],
            "SIL" => [ // 南港島綫 South Island Line
                new("ADM", "金鐘", "Admiralty"), new("OCP", "海洋公園", "Ocean Park"),
                new("WCH", "黃竹坑", "Wong Chuk Hang"), new("LET", "利東", "Lei Tung"),
                new("SOH", "海怡半島", "South Horizons")
            ],
            _ => []
        };
    }
}